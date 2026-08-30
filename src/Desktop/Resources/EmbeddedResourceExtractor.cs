using System.IO;

namespace PlanoOpenSpaceIT.Windows;

internal sealed class EmbeddedResource(string relativePath, Func<Stream> openStream)
{
    public string RelativePath { get; } = relativePath;
    public Func<Stream> OpenStream { get; } = openStream;
}

internal static class EmbeddedResourceExtractor
{
    private const string MarkerFileName = ".extracted";
    private const string IndexFileName = "index.html";

    public static string Extract(string resourcesDirectory, Guid moduleVersionId, IEnumerable<EmbeddedResource> resources)
    {
        var resourceList = resources.ToArray();
        if (IsCurrent(resourcesDirectory, moduleVersionId, resourceList)) return resourcesDirectory;

        var newResourcesDirectory = resourcesDirectory + ".new";
        if (Directory.Exists(newResourcesDirectory)) Directory.Delete(newResourcesDirectory, true);
        Directory.CreateDirectory(newResourcesDirectory);

        foreach (var resource in resourceList)
        {
            var relativePath = NormalizeRelativePath(resource.RelativePath);
            var outputPath = Path.Combine(newResourcesDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var input = resource.OpenStream();
            using var output = File.Create(outputPath);
            input.CopyTo(output);
        }

        try
        {
            if (Directory.Exists(resourcesDirectory)) Directory.Delete(resourcesDirectory, true);
            Directory.Move(newResourcesDirectory, resourcesDirectory);
            File.WriteAllText(Path.Combine(resourcesDirectory, MarkerFileName), moduleVersionId.ToString("D"));
        }
        catch when (IsCurrent(resourcesDirectory, moduleVersionId, resourceList))
        {
            return resourcesDirectory;
        }

        return resourcesDirectory;
    }

    internal static string NormalizeRelativePath(string relativePath)
    {
        var parts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || Path.IsPathRooted(relativePath) || parts.Any(part => part is "." or ".."))
            throw new InvalidDataException($"Ruta de recurso embebido no válida: {relativePath}");
        return Path.Combine(parts);
    }

    private static bool IsCurrent(string resourcesDirectory, Guid moduleVersionId, IEnumerable<EmbeddedResource> resources)
    {
        var markerPath = Path.Combine(resourcesDirectory, MarkerFileName);
        var indexPath = Path.Combine(resourcesDirectory, IndexFileName);

        try
        {
            return File.Exists(indexPath)
                && File.Exists(markerPath)
                && resources.All(resource => File.Exists(Path.Combine(resourcesDirectory, NormalizeRelativePath(resource.RelativePath))))
                && string.Equals(File.ReadAllText(markerPath).Trim(), moduleVersionId.ToString("D"), StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
