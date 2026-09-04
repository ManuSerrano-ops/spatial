using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlanoOpenSpaceIT.Windows;

internal sealed class TransactionCoordinator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly Storage _storage;

    internal TransactionCoordinator(Storage storage)
    {
        _storage = storage;
    }

    internal string Execute(
        Dictionary<string, JsonObject> documents,
        IEnumerable<string> files,
        string backupDescription,
        string eventTitle,
        string eventDescription,
        long sourceRevision,
        string? undoOf = null,
        string? seatId = null)
    {
        var transactionFiles = files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (transactionFiles.Length == 0 || transactionFiles.Any(file => !_storage.TransactionFiles.Contains(file))) throw new InvalidDataException("Conjunto de ficheros de transacción inválido.");
        if (sourceRevision != _storage.CurrentRevision()) throw new InvalidOperationException("La revisión de datos cambió antes de confirmar la operación.");

        var destinationRevision = checked(sourceRevision + 1);
        var transactionId = Guid.NewGuid().ToString("N");
        string? backupId = null;
        Dictionary<string, string>? temporaries = null;
        try
        {
            backupId = _storage.CreateBackup(transactionFiles, backupDescription);
            _storage.LogInfo(new TransactionAudit("transaction.backup", seatId, sourceRevision, destinationRevision, backupId, transactionId, transactionFiles, "created"));
            var events = documents.TryGetValue("events.json", out var eventDocument) ? eventDocument : _storage.ReadJson(_storage.DataPath("events.json")) ?? New("events");
            AddEvent(events, eventTitle, eventDescription, backupId, undoOf);
            documents["events.json"] = events;
            transactionFiles = transactionFiles.Union(["events.json"], StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var file in transactionFiles)
            {
                if (!documents.TryGetValue(file, out var document)) throw new InvalidDataException($"Falta el documento transaccional {file}.");
                StampRevision(document, destinationRevision);
            }

            temporaries = transactionFiles.ToDictionary(file => file, file => _storage.DataPath(file) + "." + transactionId + ".tmp", StringComparer.OrdinalIgnoreCase);
            foreach (var file in transactionFiles) _storage.WriteText(temporaries[file], documents[file].ToJsonString(JsonOptions));
            var pending = new JsonObject
            {
                ["schemaVersion"] = "1.0",
                ["transactionId"] = transactionId,
                ["backupId"] = backupId,
                ["sourceRevision"] = sourceRevision,
                ["destinationRevision"] = destinationRevision,
                ["files"] = new JsonArray(transactionFiles.Select(file => JsonValue.Create(file)).ToArray()),
                ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"),
                ["createdBy"] = Environment.UserName
            };
            _storage.WriteAtomic(_storage.PendingPath, pending);
            foreach (var file in transactionFiles) _storage.Move(temporaries[file], _storage.DataPath(file));
            _storage.WriteState(destinationRevision);
            _storage.Delete(_storage.PendingPath);
            _storage.LogInfo(new TransactionAudit("transaction.commit", seatId, sourceRevision, destinationRevision, backupId, transactionId, transactionFiles, "committed"));
            return backupId;
        }
        catch (Exception exception)
        {
            _storage.LogError(new TransactionAudit("transaction.failed", seatId, sourceRevision, destinationRevision, backupId, transactionId, transactionFiles, backupId is null ? "not-created" : "created"), exception);
            throw;
        }
        finally
        {
            if (temporaries is not null) foreach (var temporary in temporaries.Values) if (_storage.Exists(temporary)) _storage.Delete(temporary);
        }
    }

    internal void RecoverPending()
    {
        if (!_storage.Exists(_storage.PendingPath)) return;
        JsonObject? pending = null;
        try { pending = _storage.ReadJson(_storage.PendingPath); }
        catch (JsonException) { }
        var backupId = pending is null ? "desconocido" : Text(pending["backupId"]);
        var transactionId = pending is null ? null : Text(pending["transactionId"]);
        if (_storage.ReadOnly)
        {
            _storage.LogInfo(new TransactionAudit("recovery.pending", BackupId: backupId, TransactionId: transactionId, BackupOutcome: "read-only-blocked"));
            throw new InvalidOperationException($"Hay una recuperación pendiente del backup {backupId}. Un usuario con permisos de escritura debe abrir la aplicación para completarla o restaurarla antes de continuar.");
        }
        if (pending is null || !TryPending(pending, out var files, out var source, out var destination, out backupId))
        {
            _storage.LogInfo(new TransactionAudit("recovery.invalid", BackupId: backupId, TransactionId: transactionId, BackupOutcome: "manual-intervention-required"));
            throw new InvalidOperationException($"No se pudo recuperar la transacción pendiente. Backup requerido: {backupId}. Un operador debe comprobar la copia de seguridad y restaurar una copia válida antes de abrir los datos.");
        }
        var folder = _storage.BackupPath(backupId);
        if (!_storage.BackupExists(folder) || !_storage.BackupContainsFiles(folder, files))
        {
            _storage.LogInfo(new TransactionAudit("recovery.backup-missing", SourceRevision: source, DestinationRevision: destination, BackupId: backupId, TransactionId: transactionId, Files: files, BackupOutcome: "manual-intervention-required"));
            throw new InvalidOperationException($"No se pudo recuperar la transacción pendiente. Backup requerido: {backupId}. Un operador debe comprobar la copia de seguridad y restaurar una copia válida antes de abrir los datos.");
        }

        var revisions = files.Select(file => ReadRevision(_storage.DataPath(file))).ToArray();
        var stateRevision = _storage.CurrentRevision();
        if (revisions.All(revision => revision == destination))
        {
            _storage.WriteState(destination);
            CleanupPending(files, Text(pending["transactionId"]));
            _storage.LogInfo(new TransactionAudit("recovery.confirmed", SourceRevision: source, DestinationRevision: destination, BackupId: backupId, TransactionId: transactionId, Files: files, BackupOutcome: "confirmed"));
            return;
        }
        if (revisions.All(revision => revision != destination) && stateRevision == source)
        {
            CleanupPending(files, Text(pending["transactionId"]));
            _storage.LogInfo(new TransactionAudit("recovery.discarded", SourceRevision: source, DestinationRevision: destination, BackupId: backupId, TransactionId: transactionId, Files: files, BackupOutcome: "discarded"));
            return;
        }

        var documents = _storage.LoadBackupDocuments(folder, files);
        foreach (var document in documents.Values) StampRevision(document, destination);
        var eventDocument = documents.TryGetValue("events.json", out var backedEvents) ? backedEvents : _storage.ReadJson(_storage.DataPath("events.json")) ?? New("events");
        AddEvent(eventDocument, "Recuperación revertida", backupId, backupId, null, "reverted", source, destination);
        StampRevision(eventDocument, destination);
        documents["events.json"] = eventDocument;
        foreach (var (file, document) in documents) _storage.WriteAtomic(_storage.DataPath(file), document);
        if (_storage.DirectoryExists(folder))
        {
            var manifestPath = Path.Combine(folder, "manifest.json");
            var manifest = _storage.ReadJson(manifestPath) ?? new JsonObject { ["id"] = backupId };
            manifest["recovery"] = "reverted";
            manifest["sourceRevision"] = source;
            manifest["destinationRevision"] = destination;
            _storage.WriteAtomic(manifestPath, manifest);
        }
        _storage.WriteState(destination);
        CleanupPending(files, Text(pending["transactionId"]));
        _storage.LogInfo(new TransactionAudit("recovery.reverted", SourceRevision: source, DestinationRevision: destination, BackupId: backupId, TransactionId: transactionId, Files: files, BackupOutcome: "reverted"));
    }

    internal static void StampRevision(JsonObject document, long revision) => document["stateRevision"] = revision;

    private void CleanupPending(IEnumerable<string> files, string transactionId)
    {
        foreach (var file in files)
        {
            var temporary = _storage.DataPath(file) + "." + transactionId + ".tmp";
            if (_storage.Exists(temporary)) _storage.Delete(temporary);
        }
        if (_storage.Exists(_storage.PendingPath)) _storage.Delete(_storage.PendingPath);
    }

    private bool TryPending(JsonObject pending, out string[] files, out long source, out long destination, out string backupId)
    {
        files = pending["files"]?.AsArray().Select(Text).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        source = 0;
        destination = 0;
        backupId = Text(pending["backupId"]);
        var valid = Guid.TryParseExact(Text(pending["transactionId"]), "N", out _) && _storage.IsBackupId(backupId) && TryRevision(pending["sourceRevision"], out source) && TryRevision(pending["destinationRevision"], out destination) && destination == source + 1 && files.Length > 0 && files.All(_storage.TransactionFiles.Contains);
        return valid;
    }

    private long? ReadRevision(string path)
    {
        var document = _storage.ReadJson(path);
        return document is not null && TryRevision(document["stateRevision"], out var revision) ? revision : null;
    }

    private static void AddEvent(JsonObject document, string action, string description, string? backupId = null, string? undoOf = null, string? recovery = null, long? sourceRevision = null, long? destinationRevision = null)
    {
        var list = document["events"]?.AsArray() ?? new JsonArray();
        var item = new JsonObject { ["id"] = Guid.NewGuid().ToString("N"), ["title"] = action, ["description"] = description, ["backupId"] = backupId, ["undoOf"] = undoOf, ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"), ["createdBy"] = Environment.UserName };
        if (recovery is not null) { item["recovery"] = recovery; item["sourceRevision"] = sourceRevision; item["destinationRevision"] = destinationRevision; }
        list.Add(item);
        document["events"] = list;
    }

    private static JsonObject New(string array) => new() { ["schemaVersion"] = "1.0", ["version"] = 0, [array] = new JsonArray() };

    private static bool TryRevision(JsonNode? node, out long revision)
    {
        revision = 0;
        return node is JsonValue value && value.TryGetValue<long>(out revision) && revision >= 0;
    }

    private static string Text(JsonNode? value) => value?.ToString() ?? "";

    internal sealed record Storage(
        IReadOnlySet<string> TransactionFiles,
        bool ReadOnly,
        Func<string, string> DataPath,
        string PendingPath,
        Func<long> CurrentRevision,
        Action<long> WriteState,
        Func<string, JsonObject?> ReadJson,
        Action<string, JsonObject> WriteAtomic,
        Action<string, string> WriteText,
        Action<string, string> Move,
        Action<string> Delete,
        Func<string, bool> Exists,
        Func<string, bool> DirectoryExists,
        Func<IEnumerable<string>, string, string> CreateBackup,
        Func<string, string> BackupPath,
        Func<string, bool> BackupExists,
        Func<string, IEnumerable<string>, bool> BackupContainsFiles,
        Func<string, IEnumerable<string>, Dictionary<string, JsonObject>> LoadBackupDocuments,
        Func<string, bool> IsBackupId,
        Action<TransactionAudit> LogInfo,
        Action<TransactionAudit, Exception> LogError);

    internal sealed record TransactionAudit(
        string Action,
        string? SeatId = null,
        long? SourceRevision = null,
        long? DestinationRevision = null,
        string? BackupId = null,
        string? TransactionId = null,
        IEnumerable<string>? Files = null,
        string? BackupOutcome = null);
}
