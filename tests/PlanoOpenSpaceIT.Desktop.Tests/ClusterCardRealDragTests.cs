using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Wpf;
using Xunit;

namespace PlanoOpenSpaceIT.Desktop.Tests;

public sealed class ClusterCardRealDragTests
{
    [Fact]
    public Task RealDragPreservesCardAndWorkspaceState() => StaTest.RunAsync(() =>
    {
        var application = new Application();
        var window = new Window { Width = 920, Height = 680, Left = 20, Top = 20, Title = "ClusterCardRealDragTests" };
        var browser = new WebView2();
        window.Content = browser;
        var exitCode = 1;
        window.Loaded += async (_, _) =>
        {
            exitCode = await RunBrowserAsync(browser);
            window.Close();
            application.Shutdown();
        };
        application.Run(window);
        if (exitCode != 0) throw new InvalidOperationException("Cluster-card drag harness returned a failing exit code.");
    });

    private static async Task<int> RunBrowserAsync(WebView2 browser)
    {
        try
        {
            await browser.EnsureCoreWebView2Async();
            var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            browser.NavigationCompleted += (_, args) =>
            {
                if (args.IsSuccess) loaded.TrySetResult(true);
                else loaded.TrySetException(new InvalidOperationException($"WebView navigation failed: {args.WebErrorStatus}"));
            };

            var helperPath = Path.Combine(Environment.CurrentDirectory, "Resources", "js", "features", "managed-areas", "cluster-card-drag-helpers.js");
            if (!File.Exists(helperPath)) throw new FileNotFoundException("Missing production cluster-card drag helper.", helperPath);
            var helper = await File.ReadAllTextAsync(helperPath);
            browser.NavigateToString(BuildPage(helper));
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await Task.Delay(150);

            var before = await ReadSnapshot(browser);
            await Mouse(browser, "mousePressed", before.HandleX, before.HandleY, "left", 1);
            Xunit.Assert.True(await Value<bool>(browser, "window.handle.hasPointerCapture(window.pointerId)"), "Move handle did not capture the real pointer.");
            await Mouse(browser, "mouseMoved", before.HandleX + 300, before.HandleY + 100, "left", 1);
            await Task.Delay(80);
            var during = await ReadSnapshot(browser);
            Xunit.Assert.True(await Value<bool>(browser, "window.card === window.originalCard && window.card.isConnected && document.contains(window.card)"), "Card node was replaced during drag.");
            Xunit.Assert.True(Math.Abs(during.Left - before.Left) > 250, $"Expected >250px horizontal movement, got {during.Left - before.Left:0.0}px.");
            Xunit.Assert.True(Math.Abs(during.Top - before.Top) > 70, $"Expected >70px vertical movement, got {during.Top - before.Top:0.0}px.");
            Xunit.Assert.True(await Value<bool>(browser, "window.mapPan === 0 && window.workspaceHash === 'W-1:.1:.2|W-2:.3:.4'"), "Drag mutated map pan or workspace coordinates.");

            await Mouse(browser, "mouseReleased", before.HandleX + 300, before.HandleY + 100, "left", 0);
            await Task.Delay(80);
            var after = await ReadSnapshot(browser);
            Xunit.Assert.True(Near(after.Left, during.Left, 2) && Near(after.Top, during.Top, 2), "Card snapped after pointerup.");

            await Execute(browser, "window.rebuild();");
            await Task.Delay(80);
            var saved = await ReadSnapshot(browser);
            Xunit.Assert.True(Near(saved.Left, after.Left, 2) && Near(saved.Top, after.Top, 2), "Full rerender did not preserve saved position.");

            Console.WriteLine($"Cluster card real drag harness: PASS; delta=({during.Left - before.Left:0.0}px, {during.Top - before.Top:0.0}px), saved=({saved.Left:0.0}px, {saved.Top:0.0}px)");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Cluster card real drag harness: FAIL; {error}");
            return 1;
        }
    }

    private static string BuildPage(string helper) => """
<!doctype html><html><head><meta charset="utf-8"><style>
html,body { margin:0; overflow:hidden; } #plan { position:relative; width:800px; height:500px; background:#1f2937; } .cluster { --cluster-drag-x:0px; --cluster-drag-y:0px; position:absolute; width:180px; height:120px; box-sizing:border-box; padding:8px; border:2px solid #60a5fa; border-radius:8px; color:#fff; background:#111827; transform:translate(-50%,-50%) translate3d(var(--cluster-drag-x),var(--cluster-drag-y),0); } .cluster-move-handle { display:grid; grid-template-columns:22px 1fr auto; min-height:36px; align-items:center; cursor:grab; touch-action:none; user-select:none; -webkit-user-select:none; background:#1e3a5f; } .cluster-move-handle:active { cursor:grabbing; } .cluster-count { padding:2px 6px; border-radius:10px; background:#2563eb; }
</style></head><body><div id="plan"></div><script>__CLUSTER_CARD_DRAG_HELPER__</script><script>
window.workspaceHash='W-1:.1:.2|W-2:.3:.4'; window.mapPan=0; window.cardState={x:.5,y:.5};
const plan=document.getElementById('plan');
window.rebuild=()=>{ window.card?.remove(); const card=document.createElement('div'); card.className='cluster card-editing'; card.dataset.managedAreaId='test'; card.style.left=(window.cardState.x*100)+'%'; card.style.top=(window.cardState.y*100)+'%'; const handle=document.createElement('div'); handle.className='cluster-move-handle'; handle.textContent='⠿ test'; const badge=document.createElement('span'); badge.className='cluster-count'; badge.textContent='3'; handle.append(badge); card.append(handle); plan.append(card); window.card=card; window.handle=handle; window.originalCard=card; window.move=ClusterCardDragHelpers.attachClusterCardMoveHandle({card,handle,plan,getAnchor:()=>window.cardState,setDraftAnchor:patch=>{window.cardState={x:patch.anchorX,y:patch.anchorY};},onStateChange:state=>{if(state.phase==='start')window.pointerId=state.start.pointerId;}}); };
window.snapshot=()=>{const card=window.card.getBoundingClientRect(), handle=window.handle.getBoundingClientRect();return {left:card.left,top:card.top,handleX:handle.left+handle.width/2,handleY:handle.top+handle.height/2};}; window.rebuild();
</script></body></html>
""".Replace("__CLUSTER_CARD_DRAG_HELPER__", helper, StringComparison.Ordinal);

    private static async Task Mouse(WebView2 browser, string type, double x, double y, string? button, int buttons)
    {
        var payload = JsonSerializer.Serialize(new { type, x, y, button, buttons, clickCount = type == "mousePressed" ? 1 : 0 });
        await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", payload);
    }

    private static async Task<Snapshot> ReadSnapshot(WebView2 browser) => await Value<Snapshot>(browser, "window.snapshot()");
    private static async Task Execute(WebView2 browser, string script) => await browser.ExecuteScriptAsync(script);

    private static async Task<T> Value<T>(WebView2 browser, string script)
    {
        var json = await browser.ExecuteScriptAsync(script);
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"No value returned for: {script}");
    }

    private static bool Near(double left, double right, double tolerance) => Math.Abs(left - right) <= tolerance;

    private sealed record Snapshot(double Left, double Top, double HandleX, double HandleY);
}
