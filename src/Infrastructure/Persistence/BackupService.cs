using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PlanoOpenSpaceIT.Windows;

internal sealed class BackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly Storage _storage;

    internal BackupService(Storage storage)
    {
        _storage = storage;
    }

    internal string Create(IEnumerable<string> files, string description)
    {
        var transactionFiles = files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (transactionFiles.Any(file => !_storage.TransactionFiles.Contains(file))) throw new InvalidDataException("Conjunto de ficheros de backup inválido.");
        var id = DateTime.Now.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N")[..6];
        Directory.CreateDirectory(_storage.BackupsRoot);
        var archivePath = Path.Combine(_storage.BackupsRoot, id + ".zip");
        var createdAtUtc = DateTimeOffset.UtcNow;
        var manifest = new JsonObject
        {
            ["id"] = id,
            ["title"] = "Backup",
            ["description"] = description,
            ["files"] = new JsonArray(_storage.OperationalFiles.Select(file => JsonValue.Create(file)).ToArray()),
            ["transactionFiles"] = new JsonArray(transactionFiles.Select(file => JsonValue.Create(file)).ToArray()),
            ["sourceRevision"] = _storage.CurrentRevision(),
            ["createdAt"] = createdAtUtc.ToString("O"),
            ["createdAtUtc"] = createdAtUtc.ToString("O"),
            ["createdBy"] = Environment.UserName
        };
        var temporaryPath = archivePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
            {
                foreach (var file in _storage.OperationalFiles)
                {
                    var source = _storage.DataPath(file);
                    if (File.Exists(source)) archive.CreateEntryFromFile(source, file, CompressionLevel.Optimal);
                    else if (string.Equals(file, ManagedAreas.FileName, StringComparison.OrdinalIgnoreCase))
                    {
                        var emptyManagedAreas = ManagedAreas.EmptyDocument();
                        emptyManagedAreas["stateRevision"] = _storage.CurrentRevision();
                        WriteZipJson(archive, file, emptyManagedAreas);
                    }
                }
                var state = _storage.StatePath;
                if (File.Exists(state)) archive.CreateEntryFromFile(state, "state.origin.json", CompressionLevel.Optimal);
                WriteZipJson(archive, "manifest.json", manifest);
            }
            using (var validation = ZipFile.OpenRead(temporaryPath))
                if (validation.GetEntry("manifest.json") is null) throw new InvalidDataException("El backup temporal no contiene manifiesto.");
            File.Move(temporaryPath, archivePath);
            return id;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    internal IEnumerable<(string Id, string Container, JsonObject Manifest, bool Legacy)> Containers()
    {
        if (!Directory.Exists(_storage.BackupsRoot)) return [];
        var containers = new List<(string Id, string Container, JsonObject Manifest, bool Legacy)>();
        foreach (var container in Directory.GetDirectories(_storage.BackupsRoot).Concat(Directory.GetFiles(_storage.BackupsRoot, "*.zip")))
        {
            try
            {
                var manifest = ReadManifest(container);
                if (manifest is null) continue;
                containers.Add((Text(manifest["id"]), container, manifest, Directory.Exists(container) && manifest["files"] is not JsonArray));
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
            {
                _storage.LogInvalidManifest(exception, Path.GetFileName(container));
            }
        }
        return containers
            .OrderByDescending(item => CreatedAtUtc(item.Manifest) ?? DateTimeOffset.MinValue)
            .ThenByDescending(item => item.Id, StringComparer.Ordinal)
            .ToArray();
    }

    internal string ResolvePath(string id)
    {
        if (!IsId(id)) throw new InvalidDataException("Formato de backup inválido.");
        var root = Path.GetFullPath(_storage.BackupsRoot);
        var candidate = Path.GetFullPath(Path.Combine(root, id));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ruta de backup inválida.");
        var archive = candidate + ".zip";
        return File.Exists(archive) ? archive : candidate;
    }

    internal bool Exists(string container) => Directory.Exists(container) || File.Exists(container);

    internal bool ContainsFiles(string container, IEnumerable<string> files)
    {
        if (File.Exists(container))
        {
            using var archive = ZipFile.OpenRead(container);
            return files.All(file => archive.GetEntry(file) is not null);
        }
        return files.All(file => File.Exists(Path.Combine(container, file)));
    }

    internal string[] Files(string container) => ManifestFiles(container, "files", _storage.OperationalFiles);

    internal string[] TransactionFiles(string container) => ManifestFiles(container, "transactionFiles", _storage.TransactionFiles);

    internal Dictionary<string, JsonObject> LoadDocuments(string container, IEnumerable<string> files)
    {
        var documents = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(container))
        {
            using var archive = ZipFile.OpenRead(container);
            foreach (var file in files)
            {
                var entry = archive.GetEntry(file) ?? throw new FileNotFoundException($"Falta {file} en el backup.");
                using var stream = entry.Open();
                documents[file] = JsonNode.Parse(stream)?.AsObject() ?? throw new InvalidDataException($"{file} no es válido.");
            }
            return documents;
        }
        foreach (var file in files)
        {
            var document = ReadJson(Path.Combine(container, file));
            if (document is null) throw new FileNotFoundException($"Falta {file} en el backup.");
            documents[file] = document;
        }
        return documents;
    }

    internal JsonObject RetentionReportDocument(BackupRetentionReportData report)
    {
        JsonObject Entry(BackupRetentionReportEntry item) => new()
        {
            ["id"] = item.Id,
            ["createdAtUtc"] = item.CreatedAtUtc.ToString("O"),
            ["sizeBytes"] = item.SizeBytes,
            ["legacy"] = item.IsLegacy,
            ["undoProtected"] = item.IsProtected,
            ["retain"] = item.Retain,
            ["reason"] = item.Reason
        };

        return new JsonObject
        {
            ["generatedAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["retentionMode"] = "report",
            ["totalBackups"] = report.TotalBackups,
            ["totalBytes"] = report.TotalBytes,
            ["oldestCreatedAtUtc"] = report.OldestCreatedAtUtc?.ToString("O"),
            ["undoProtectedCount"] = report.UndoProtectedCount,
            ["reclaimableBytes"] = report.ReclaimableBytes,
            ["backups"] = new JsonArray(report.Backups.Select(Entry).ToArray()),
            ["candidates"] = new JsonArray(report.Backups.Where(item => !item.Retain).Select(Entry).ToArray())
        };
    }

    internal void WriteRetentionReport(JsonObject document, int totalBackups, long reclaimableBytes)
    {
        var audit = new RetentionReportAudit("backup.retention.report", totalBackups, "report");
        try
        {
            _storage.CreateDirectory(_storage.LogsRoot);
            var path = Path.Combine(_storage.LogsRoot, $"backup-retention-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffffffZ}.json");
            document["reportPath"] = path;
            _storage.WriteText(path, document.ToJsonString(JsonOptions));
            _storage.LogRetentionInfo(audit with { ReportPath = path });
        }
        catch (Exception exception)
        {
            document.Remove("reportPath");
            _storage.LogRetentionError(audit with { Action = "backup.retention.report.failed" }, exception);
        }
    }

    internal static DateTimeOffset? CreatedAtUtc(JsonObject manifest)
    {
        if (DateTimeOffset.TryParse(Text(manifest["createdAtUtc"]), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc)) return utc;
        if (!DateTime.TryParse(Text(manifest["createdAt"]), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var legacy)) return null;
        return new DateTimeOffset(DateTime.SpecifyKind(legacy, DateTimeKind.Local)).ToUniversalTime();
    }

    internal static bool IsId(string id) => Regex.IsMatch(id, "^[0-9]{17}-[0-9a-fA-F]{6}$");

    private string[] ManifestFiles(string container, string property, IEnumerable<string> allowed)
    {
        var manifest = ReadManifest(container);
        var files = manifest?[property]?.AsArray().Select(Text).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (files is null || files.Length == 0) files = ["maps.json", "assignments.json", "positions.json"];
        if (files.Any(file => !allowed.Contains(file, StringComparer.OrdinalIgnoreCase))) throw new InvalidDataException("El backup contiene ficheros no compatibles.");
        return files;
    }

    private static JsonObject? ReadManifest(string container)
    {
        if (File.Exists(container))
        {
            using var archive = ZipFile.OpenRead(container);
            var entry = archive.GetEntry("manifest.json");
            if (entry is null) return null;
            using var stream = entry.Open();
            return JsonNode.Parse(stream)?.AsObject();
        }
        return ReadJson(Path.Combine(container, "manifest.json"));
    }

    private static JsonObject? ReadJson(string path) => File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))?.AsObject() : null;

    private static void WriteZipJson(ZipArchive archive, string name, JsonObject value)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(value.ToJsonString(JsonOptions));
    }

    private static string Text(JsonNode? value) => value?.ToString() ?? "";

    internal sealed record Storage(
        IReadOnlySet<string> OperationalFiles,
        IReadOnlySet<string> TransactionFiles,
        string BackupsRoot,
        string StatePath,
        Func<string, string> DataPath,
        Func<long> CurrentRevision,
        Action<Exception, string> LogInvalidManifest,
        string LogsRoot,
        Action<string> CreateDirectory,
        Action<string, string> WriteText,
        Action<RetentionReportAudit> LogRetentionInfo,
        Action<RetentionReportAudit, Exception> LogRetentionError);

    internal sealed record RetentionReportAudit(
        string Action,
        int Count,
        string BackupOutcome,
        string? ReportPath = null);
}
