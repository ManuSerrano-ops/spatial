using System.IO;
using System.Text.Json;

namespace PlanoOpenSpaceIT.Windows;

internal sealed record ExportFolderPreferences(
    string? ExportFolder,
    bool SkipExportFolderPrompt,
    string? Theme = null,
    bool SingleKeyShortcutsEnabled = true,
    double? WindowWidth = null,
    double? WindowHeight = null,
    double? WindowLeft = null,
    double? WindowTop = null,
    string? WindowState = null);
internal sealed record ExportFolderChoice(string Folder, bool UseAlways);
internal sealed record ExportFolderResolution(bool Cancelled, string? Folder)
{
    internal static ExportFolderResolution CancelledByUser() => new(true, null);
    internal static ExportFolderResolution Selected(string folder) => new(false, folder);
}

internal interface IUserPreferencesStore
{
    ExportFolderPreferences Load();
    void Save(ExportFolderPreferences preferences);
}

internal interface IExportFolderDialog
{
    ExportFolderChoice? Choose(string initialFolder);
    void ShowFolderNotWritable(string folder);
}

internal sealed class UserPreferencesStore : IUserPreferencesStore
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private readonly string _path;

    internal UserPreferencesStore(string? path = null) => _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlanoOpenSpaceITUiFigma", "user-preferences.json");

    public ExportFolderPreferences Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<ExportFolderPreferences>(File.ReadAllText(_path), Options) ?? new ExportFolderPreferences(null, false)
                : new ExportFolderPreferences(null, false);
        }
        catch (Exception) { return new ExportFolderPreferences(null, false); }
    }

    internal string LoadTheme() => UiThemes.Normalize(Load().Theme);

    internal bool LoadSingleKeyShortcutsEnabled() => Load().SingleKeyShortcutsEnabled;

    internal void SaveUserPreferences(string? theme, bool? singleKeyShortcutsEnabled)
    {
        var preferences = Load();
        Save(preferences with
        {
            Theme = theme is null ? preferences.Theme : UiThemes.Normalize(theme),
            SingleKeyShortcutsEnabled = singleKeyShortcutsEnabled ?? preferences.SingleKeyShortcutsEnabled
        });
    }

    internal void SaveWindowPlacement(WindowBounds bounds, bool isMaximized)
    {
        if (!bounds.IsFiniteAndPositive) return;

        var preferences = Load();
        Save(preferences with
        {
            WindowWidth = bounds.Width,
            WindowHeight = bounds.Height,
            WindowLeft = bounds.Left,
            WindowTop = bounds.Top,
            WindowState = isMaximized ? nameof(System.Windows.WindowState.Maximized) : nameof(System.Windows.WindowState.Normal)
        });
    }

    public void Save(ExportFolderPreferences preferences)
    {
        var folder = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("Ruta de preferencias inválida.");
        Directory.CreateDirectory(folder);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(preferences, Options));
        File.Move(temporary, _path, true);
    }
}

internal sealed class ExportFolderResolver
{
    private readonly IUserPreferencesStore _preferences;
    private readonly IExportFolderDialog _dialog;
    private readonly Func<string, bool> _isWritable;
    private readonly string _documentsFolder;

    internal ExportFolderResolver(IUserPreferencesStore preferences, IExportFolderDialog dialog, Func<string, bool>? isWritable = null, string? documentsFolder = null)
    {
        _preferences = preferences;
        _dialog = dialog;
        _isWritable = isWritable ?? IsWritable;
        _documentsFolder = documentsFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    internal ExportFolderResolution Resolve()
    {
        var preferences = _preferences.Load();
        if (preferences.SkipExportFolderPrompt && IsUsable(preferences.ExportFolder)) return ExportFolderResolution.Selected(preferences.ExportFolder!);

        var initialFolder = IsUsable(preferences.ExportFolder) ? preferences.ExportFolder! : _documentsFolder;
        while (true)
        {
            var choice = _dialog.Choose(initialFolder);
            if (choice is null) return ExportFolderResolution.CancelledByUser();
            if (_isWritable(choice.Folder))
            {
                _preferences.Save(preferences with { ExportFolder = choice.Folder, SkipExportFolderPrompt = choice.UseAlways });
                return ExportFolderResolution.Selected(choice.Folder);
            }
            _dialog.ShowFolderNotWritable(choice.Folder);
            initialFolder = choice.Folder;
        }
    }

    private bool IsUsable(string? folder)
    {
        return !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder) && _isWritable(folder);
    }

    private static bool IsWritable(string folder)
    {
        try
        {
            if (!Directory.Exists(folder)) return false;
            var probe = Path.Combine(folder, ".plano-open-space-write-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (File.Create(probe)) { }
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }
}

internal sealed class WindowsExportFolderDialog : IExportFolderDialog
{
    public ExportFolderChoice? Choose(string initialFolder)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            InitialDirectory = initialFolder,
            Description = "Elige la carpeta para exportar el Excel",
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath)) return null;
        var useAlways = System.Windows.MessageBox.Show(
            "¿Usar siempre esta carpeta sin volver a preguntar?",
            "Exportar Excel",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;
        return new ExportFolderChoice(dialog.SelectedPath, useAlways);
    }

    public void ShowFolderNotWritable(string folder) => System.Windows.MessageBox.Show(
        $"No se puede escribir en la carpeta seleccionada:\n{folder}\n\nElige otra carpeta.",
        "Exportar Excel",
        System.Windows.MessageBoxButton.OK,
        System.Windows.MessageBoxImage.Warning);
}
