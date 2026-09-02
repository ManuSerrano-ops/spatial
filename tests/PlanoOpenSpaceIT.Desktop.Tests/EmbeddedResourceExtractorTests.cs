using System.Collections;
using System.Reflection;
using Xunit;

namespace PlanoOpenSpaceIT.Desktop.Tests;

public sealed class EmbeddedResourceExtractorTests
{
    [Fact]
    public void ExtractsEveryLightThemeResourceWithItsLogicalPath()
    {
        var assembly = Assembly.Load("PlanoOpenSpaceIT.Windows");
        const string marker = ".Resources.";
        var expected = new[]
        {
            "map-themes/light/manifest.json",
            "map-themes/light/plano_norte_limpio.svg",
            "map-themes/light/plano_nivel3_limpio.svg",
            "map-themes/light/plano_sur_limpio.svg",
            "map-themes/light/plano_id.svg",
            "map-themes/light/plano_qc_limpio.svg"
        };
        var selected = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(marker, StringComparison.Ordinal))
            .Select(name => (Name: name, Relative: name[(name.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..]))
            .Where(item => expected.Contains(item.Relative, StringComparer.Ordinal))
            .ToArray();

        Assert.Equal(expected.Length, selected.Length);
        Assert.True(expected.All(path => selected.Any(item => item.Relative == path)), "Every Light logical name preserves map-themes/light.");

        var resourceType = assembly.GetType("PlanoOpenSpaceIT.Windows.EmbeddedResource", throwOnError: true)!;
        var extractorType = assembly.GetType("PlanoOpenSpaceIT.Windows.EmbeddedResourceExtractor", throwOnError: true)!;
        var constructor = resourceType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        var resources = Array.CreateInstance(resourceType, selected.Length);
        for (var index = 0; index < selected.Length; index++)
        {
            var item = selected[index];
            Func<Stream> open = () => assembly.GetManifestResourceStream(item.Name)
                ?? throw new FileNotFoundException($"Missing embedded stream {item.Name}.");
            resources.SetValue(constructor.Invoke(new object?[] { item.Relative, open }), index);
        }

        var extract = extractorType.GetMethod("Extract", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(extractorType.FullName, "Extract");
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"light-map-extraction-{Guid.NewGuid():N}");
        try
        {
            var extracted = (string?)extract.Invoke(null, new object?[] { temporaryRoot, Guid.NewGuid(), resources })
                ?? throw new InvalidOperationException("Extractor returned no directory.");
            Assert.All(expected, relativePath =>
                Assert.True(File.Exists(Path.Combine(extracted, relativePath.Replace('/', Path.DirectorySeparatorChar))),
                    $"Extracted path is missing: {relativePath}"));
        }
        finally
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, recursive: true);
            if (Directory.Exists(temporaryRoot + ".new")) Directory.Delete(temporaryRoot + ".new", recursive: true);
        }
    }
}
