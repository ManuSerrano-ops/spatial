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
    private readonly WindowInitialization<DataStore, WebViewBridge> _initialization;
    private readonly UserPreferencesStore _userPreferences = new();
    private readonly ExportFolderResolver _exportFolderResolver;
    private bool _isClosing;

    public MainWindow()
    {
        _initialization = new WindowInitialization<DataStore, WebViewBridge>(
            () => DataStore.Create(OnAuditLogAvailabilityChanged),
            store => new WebViewBridge(store),
            store => store.LogLifecycleStart(),
            SubscribeWebMessages);
        _exportFolderResolver = new ExportFolderResolver(_userPreferences, new WindowsExportFolderDialog());
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => RestoreWindowPlacement();

    private async void OnLoaded(object sender, RoutedEventArgs e) => await InitializeAsync();

    private async void OnRetry(object sender, RoutedEventArgs e) => await InitializeAsync();

    private void OnAuditLogAvailabilityChanged(AuditLogAvailability availability)
    {
        if (!Dispatcher.CheckAccess())
        {
            if (!Dispatcher.HasShutdownStarted) _ = Dispatcher.BeginInvoke(() => OnAuditLogAvailabilityChanged(availability));
            return;
        }

        AuditLogWarning.Visibility = availability == AuditLogAvailability.Unavailable ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task InitializeAsync()
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
        try
        {
            await _initialization.InitializeAsync(InitializeWebViewAsync);
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"No se pudo iniciar Plano Open Space IT.\n\n{UserFacingError(ex)}";
            ErrorPanel.Visibility = Visibility.Visible;
            ErrorPanel.Focus();
        }
    }

    private void OnOpenLogs(object sender, RoutedEventArgs e)
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(configPath)) ?? throw new InvalidDataException();
            var logsPath = Path.Combine(config.NetworkRoot, config.LogsFolder);
            Directory.CreateDirectory(logsPath);
            Process.Start(new ProcessStartInfo(logsPath) { UseShellExecute = true });
        }
        catch
        {
            ErrorText.Text = "No se pudo abrir la carpeta de logs.";
            ErrorPanel.Focus();
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        PersistWindowPlacement();
        _isClosing = true;
        if (Browser.CoreWebView2 is not null) Browser.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
        _initialization.Store?.LogLifecycleClosing();
    }

    private void RestoreWindowPlacement()
    {
        var preferences = _userPreferences.Load();
        if (preferences.WindowWidth is not double width
            || preferences.WindowHeight is not double height
            || preferences.WindowLeft is not double left
            || preferences.WindowTop is not double top) return;

        var screens = System.Windows.Forms.Screen.AllScreens;
        var workingAreas = screens.Select(screen => ToWindowBounds(screen.WorkingArea)).ToArray();
        var primaryScreen = screens.FirstOrDefault(screen => screen.Primary);
        var primaryWorkingArea = primaryScreen is null ? default : ToWindowBounds(primaryScreen.WorkingArea);
        var restored = WindowGeometry.Clamp(new WindowBounds(left, top, width, height), workingAreas, primaryWorkingArea);

        Left = restored.Left;
        Top = restored.Top;
        Width = restored.Width;
        Height = restored.Height;
        if (string.Equals(preferences.WindowState, nameof(WindowState.Maximized), StringComparison.Ordinal)) WindowState = WindowState.Maximized;
    }

    private void PersistWindowPlacement()
    {
        try
        {
            var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
            _userPreferences.SaveWindowPlacement(new WindowBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height), WindowState == WindowState.Maximized);
        }
        catch
        {
            // Closing must not be blocked when local UI preferences cannot be written.
        }
    }

    private static WindowBounds ToWindowBounds(System.Drawing.Rectangle area) => new(area.Left, area.Top, area.Width, area.Height);

    private async Task InitializeWebViewAsync(Action subscribeWebMessages)
    {
        Browser.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlanoOpenSpaceITUiFigma", "WebView2")
        };
        await Browser.EnsureCoreWebView2Async();
        subscribeWebMessages();
        var resourcesPath = ExtractEmbeddedResources();
        var index = Path.Combine(resourcesPath, "index.html");
        if (!File.Exists(index)) throw new FileNotFoundException("No se encontró index.html en los recursos embebidos.", index);
        Browser.CoreWebView2.SetVirtualHostNameToFolderMapping("plano.local", resourcesPath, CoreWebView2HostResourceAccessKind.Allow);
        Browser.Source = new Uri("https://plano.local/index.html");
    }

    private void SubscribeWebMessages() => Browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

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
            _initialization.Store?.LogBridgeAction("untrustedOrigin", success: false, durationMs: 0);
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
            if (action == "getUserPreferences")
            {
                Reply($"{action}Result", true, UserPreferencesJson(), null);
                return;
            }
            if (action == "saveUserPreferences")
            {
                _userPreferences.SaveUserPreferences(
                    payload["theme"]?.GetValue<string>(),
                    payload["singleKeyShortcutsEnabled"]?.GetValue<bool>());
                Reply($"{action}Result", true, UserPreferencesJson(), null);
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
            if (!dispatched) _initialization.Store?.LogBridgeAction("unknown", success: false, stopwatch.ElapsedMilliseconds);
            if (!_isClosing) Reply($"{action}Result", false, null, UserFacingError(ex));
        }
    }

    private JsonObject UserPreferencesJson() => new()
    {
        ["theme"] = _userPreferences.LoadTheme(),
        ["singleKeyShortcutsEnabled"] = _userPreferences.LoadSingleKeyShortcutsEnabled()
    };

    private JsonNode Dispatch(string action, JsonObject payload) => Bridge.Dispatch(action, payload);

    private static string UserFacingError(Exception ex) => ex switch
    {
        InvalidDataException or InvalidOperationException => ex.Message,
        UnauthorizedAccessException => "No hay permisos suficientes sobre la carpeta de datos.",
        IOException => "No se pudo acceder a la carpeta de datos. Comprueba la conexión de red.",
        TimeoutException => "Otra ventana está usando los datos. Inténtalo de nuevo en unos segundos.",
        _ => "Se produjo un error inesperado. Revisa el log de auditoría."
    };

    private void UpdateWindowTitle(JsonNode? data)
    {
        var scenario = data?["activeScenario"]?.AsObject();
        var title = scenario is null
            ? "Plano Open Space IT — REALIDAD"
            : $"Plano Open Space IT — Escenario: {scenario["name"]?.GetValue<string>() ?? scenario["id"]?.GetValue<string>()}";
        if (data?["readOnly"]?.GetValue<bool>() == true) title += " (solo lectura)";
        Title = title;
    }

    private WebViewBridge Bridge => _initialization.Bridge ?? throw new InvalidOperationException("El almacén de datos no se ha inicializado.");

    private void Reply(string action, bool success, JsonNode? data, string? error)
    {
        if (_isClosing || Browser.CoreWebView2 is null) return;
        if (success && (action == "loadInitialDataResult" || action == "reloadDataResult")) UpdateWindowTitle(data);
        var reply = new JsonObject { ["action"] = action, ["success"] = success, ["data"] = data, ["error"] = error };
        var json = JsonSerializer.Serialize(reply, _jsonOptions);
        Browser.CoreWebView2.PostWebMessageAsJson(json);
    }
}
