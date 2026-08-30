namespace PlanoOpenSpaceIT.Windows;

internal static class UiThemes
{
    internal const string ProfessionalLight = "professional-light";
    internal const string PenpotDark = "penpot-dark";
    internal const string HighContrast = "high-contrast";
    internal const string Projector = "projector";

    private static readonly HashSet<string> Known =
    [
        ProfessionalLight,
        PenpotDark,
        HighContrast,
        Projector
    ];

    internal static string Normalize(string? theme) =>
        !string.IsNullOrWhiteSpace(theme) && Known.Contains(theme)
            ? theme
            : ProfessionalLight;
}
