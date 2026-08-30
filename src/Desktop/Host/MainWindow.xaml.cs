using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace PlanoOpenSpaceIT.Windows;

public partial class MainWindow : Window
{
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private DataStore? _store;
    private WebViewBridge? _bridge;
    private readonly UserPreferencesStore _userPreferences = new();
    private readonly ExportFolderResolver _exportFolderResolver;
    private bool _isClosing;

    public MainWindow()
    {
        _exportFolderResolver = new ExportFolderResolver(_userPreferences, new WindowsExportFolderDialog());
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _store = DataStore.Create();
            _bridge = new WebViewBridge(_store);
            _store.LogLifecycleStart();
            Browser.CreationProperties = new CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlanoOpenSpaceITUiFigma", "WebView2")
            };
            await Browser.EnsureCoreWebView2Async();
            Browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            var resourcesPath = ExtractEmbeddedResources();
            var index = Path.Combine(resourcesPath, "index.html");
            if (!File.Exists(index)) throw new FileNotFoundException("No se encontró index.html en los recursos embebidos.", index);
            Browser.CoreWebView2.SetVirtualHostNameToFolderMapping("plano.local", resourcesPath, CoreWebView2HostResourceAccessKind.Allow);
            Browser.Source = new Uri("https://plano.local/index.html");
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"No se pudo iniciar Plano Open Space IT.\n\n{ex.Message}";
            ErrorPanel.Visibility = Visibility.Visible;
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _isClosing = true;
        if (Browser.CoreWebView2 is not null) Browser.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
        _store?.LogLifecycleClosing();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Browser.Dispose();
    }


    private static string ExtractEmbeddedResources()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PlanoOpenSpaceITUiFigma",
            "Resources");
        const string marker = ".Resources.";

        return EmbeddedResourceExtractor.Extract(
            outputDirectory,
            assembly.ManifestModule.ModuleVersionId,
            assembly.GetManifestResourceNames()
                .Where(name => name.Contains(marker, StringComparison.Ordinal))
                .Select(resourceName => new EmbeddedResource(
                    resourceName[(resourceName.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..],
                    () => assembly.GetManifestResourceStream(resourceName)
                        ?? throw new FileNotFoundException($"No se pudo abrir el recurso embebido {resourceName}."))));
    }

    internal static bool IsTrustedWebMessageSource(string? source) => Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, "plano.local", StringComparison.OrdinalIgnoreCase);

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_isClosing) return;
        if (!IsTrustedWebMessageSource(e.Source))
        {
            _store?.LogBridgeAction("untrustedOrigin", success: false, durationMs: 0);
            return;
        }
        string action = "unknown";
        var dispatched = false;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var message = JsonNode.Parse(e.WebMessageAsJson)?.AsObject() ?? throw new InvalidDataException("Mensaje inválido.");
            action = message["action"]?.GetValue<string>() ?? throw new InvalidDataException("Falta la acción.");
            var payload = message["payload"]?.AsObject() ?? new JsonObject();
            if (action == "getThemePreference")
            {
                Reply($"{action}Result", true, new JsonObject { ["theme"] = _userPreferences.LoadTheme() }, null);
                return;
            }
            if (action == "saveThemePreference")
            {
                _userPreferences.SaveTheme(payload["theme"]?.GetValue<string>());
                Reply($"{action}Result", true, new JsonObject { ["theme"] = _userPreferences.LoadTheme() }, null);
                return;
            }
            if (action == "exportExcel")
            {
                var resolution = _exportFolderResolver.Resolve();
                if (resolution.Cancelled)
                {
                    Reply($"{action}Result", true, new JsonObject { ["cancelled"] = true }, null);
                    return;
                }
                payload["exportFolder"] = resolution.Folder;
            }
            dispatched = true;
            var data = await Task.Run(() => Dispatch(action, payload));
            if (_isClosing) return;
            Reply($"{action}Result", true, data, null);
        }
        catch (Exception ex)
        {
            if (!dispatched) _store?.LogBridgeAction("unknown", success: false, stopwatch.ElapsedMilliseconds);
            if (!_isClosing) Reply($"{action}Result", false, null, ex.Message);
        }
    }

    private JsonNode Dispatch(string action, JsonObject payload) => Bridge.Dispatch(action, payload);

    private WebViewBridge Bridge => _bridge ?? throw new InvalidOperationException("El almacén de datos no se ha inicializado.");

    private void Reply(string action, bool success, JsonNode? data, string? error)
    {
        if (_isClosing || Browser.CoreWebView2 is null) return;
        var reply = new JsonObject { ["action"] = action, ["success"] = success, ["data"] = data, ["error"] = error };
        var json = JsonSerializer.Serialize(reply, _jsonOptions);
        Browser.CoreWebView2.PostWebMessageAsJson(json);
    }
}
