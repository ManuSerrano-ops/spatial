using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using PlanoOpenSpaceIT.Windows;
using Xunit;

namespace PlanoOpenSpaceIT.Desktop.Tests;

public sealed class BackupCharacterizationTests
{
    private static readonly string[] OperationalFiles = ["maps.json", "assignments.json", "positions.json", "events.json", "scenarios.json", "people.json", "devices.json", "locations.json", "managed-areas.json"];

    [Fact]
    public void CreatedTransactionBackupContainsOperationalDocumentsOriginAndManifest()
    {
        using var fixture = new BackupFixture();

        fixture.Store.SaveAssignment(new JsonObject { ["workstationId"] = "N-1", ["personId"] = "updated", ["status"] = "confirmed" }, false);
        var backupPath = fixture.SingleZipPath();

        using var archive = ZipFile.OpenRead(backupPath);
        var manifest = ReadArchiveJson(archive, "manifest.json");
        Assert.Equal(OperationalFiles.Order(StringComparer.Ordinal), manifest["files"]!.AsArray().Select(Text).Order(StringComparer.Ordinal));
        Assert.Equal(["assignments.json", "events.json", "maps.json", "positions.json"], manifest["transactionFiles"]!.AsArray().Select(Text).Order(StringComparer.Ordinal));
        Assert.All(OperationalFiles, file => Assert.NotNull(archive.GetEntry(file)));
        Assert.NotNull(archive.GetEntry("state.origin.json"));
        Assert.Equal(0, manifest["sourceRevision"]?.GetValue<long>());
    }

    [Fact]
    public void GetBackupsSortsCurrentAndLegacyFormatsSkipsUnreadableArchivesAndListsStructuralManifest()
    {
        using var fixture = new BackupFixture();
        fixture.CreateZipBackup("20260202000000000-a1b2c3", createdAtUtc: "2026-02-02T00:00:00+00:00");
        fixture.CreateLegacyDirectoryBackup("20260101000000000-b2c3d4", createdAt: "2026-01-01T00:00:00");
        fixture.CreateZipBackup("20250101000000000-c3d4e5", manifest: new JsonObject());
        File.WriteAllText(Path.Combine(fixture.BackupsRoot, "unreadable.zip"), "not a zip archive");

        var backups = fixture.Store.GetBackups()["backups"]!.AsArray().OfType<JsonObject>().ToArray();

        Assert.Equal(["20260202000000000-a1b2c3", "20260101000000000-b2c3d4", ""], backups.Select(item => Text(item["id"])));
        Assert.DoesNotContain(backups, item => Text(item["id"]) == "unreadable");
        Assert.Contains(backups, item => item["id"] is null && item["createdAt"] is null);
    }

    [Fact]
    public void RestoreBackupReadsCurrentZipAndPreservesNonRestorableDocuments()
    {
        using var fixture = new BackupFixture();
        var backupId = "20260202000000000-a1b2c3";
        fixture.CreateZipBackup(backupId, assignmentPerson: "archived", peopleId: "archived-person");

        fixture.Store.RestoreBackup(new JsonObject { ["backupId"] = backupId });

        Assert.Equal("archived", fixture.AssignmentPerson());
        Assert.Equal("current-person", fixture.PeopleId());
        Assert.Equal("Norte archivado", fixture.MapName());
    }

    [Fact]
    public void RestoreBackupReadsLegacyDirectoryUsingImplicitFileList()
    {
        using var fixture = new BackupFixture();
        var backupId = "20260101000000000-b2c3d4";
        fixture.CreateLegacyDirectoryBackup(backupId, createdAt: "2026-01-01T00:00:00", assignmentPerson: "legacy", mapName: "Norte heredado");

        fixture.Store.RestoreBackup(new JsonObject { ["backupId"] = backupId });

        Assert.Equal("legacy", fixture.AssignmentPerson());
        Assert.Equal("Norte heredado", fixture.MapName());
    }

    [Fact]
    public void RestoreBackupRejectsTraversalIdWithoutWrites()
    {
        using var fixture = new BackupFixture();

        TestAssertions.AssertRejectedWithoutWrites(
            fixture.DataHashes,
            fixture.BackupCountOnDisk,
            () => fixture.Store.RestoreBackup(new JsonObject { ["backupId"] = "../outside" }),
            "Formato de backup inválido");
    }

    [Fact]
    public void RestoreBackupRejectsManifestWithUnsupportedFileWithoutWrites()
    {
        using var fixture = new BackupFixture();
        var backupId = "20260202000000000-d4e5f6";
        fixture.CreateZipBackupWithUnsupportedFile(backupId);

        TestAssertions.AssertRejectedWithoutWrites(
            fixture.DataHashes,
            fixture.BackupCountOnDisk,
            () => fixture.Store.RestoreBackup(new JsonObject { ["backupId"] = backupId }),
            "El backup contiene ficheros no compatibles");
    }

    [Fact]
    public void RetentionReportUsesUtcAndLegacyLocalTimestamps()
    {
        using var fixture = new BackupFixture(retentionMode: "report");
        fixture.CreateZipBackup("20260202000000000-a1b2c3", createdAtUtc: "2026-02-02T10:00:00+00:00");
        fixture.CreateLegacyDirectoryBackup("20260101000000000-b2c3d4", createdAt: "2026-01-01T00:00:00");

        var report = fixture.Store.GetBackupRetentionReport();
        var backups = report["backups"]!.AsArray().OfType<JsonObject>().ToDictionary(item => Text(item["id"]), StringComparer.Ordinal);
        var expectedLegacyUtc = new DateTimeOffset(DateTime.SpecifyKind(DateTime.Parse("2026-01-01T00:00:00"), DateTimeKind.Local)).ToUniversalTime();

        Assert.Equal(DateTimeOffset.Parse("2026-02-02T10:00:00+00:00"), DateTimeOffset.Parse(Text(backups["20260202000000000-a1b2c3"]["createdAtUtc"])));
        Assert.Equal(expectedLegacyUtc, DateTimeOffset.Parse(Text(backups["20260101000000000-b2c3d4"]["createdAtUtc"])));
        Assert.True(backups["20260101000000000-b2c3d4"]["legacy"]?.GetValue<bool>() == true);

        var reportPath = Text(report["reportPath"]);
        Assert.True(File.Exists(reportPath), "The retention report is persisted under logs.");
        var audit = Assert.Single(fixture.AuditEntries(), entry => Text(entry["action"]) == "backup.retention.report");
        Assert.Equal("information", Text(audit["level"]));
        Assert.Equal(2, audit["count"]?.GetValue<int>());
        Assert.Equal("report", Text(audit["backupOutcome"]));
        Assert.Equal(Path.GetFileName(reportPath), Text(audit["reportFile"]));
    }

    private sealed class BackupFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "backup-characterization-" + Guid.NewGuid().ToString("N"));
        private readonly string _data;

        internal BackupFixture(string retentionMode = "disabled")
        {
            _data = Path.Combine(_root, "data");
            Directory.CreateDirectory(_data);
            Directory.CreateDirectory(BackupsRoot);
            Write("maps.json", Maps("Norte actual"));
            Write("assignments.json", Assignments("current"));
            Write("positions.json", Positions());
            Write("events.json", new JsonObject { ["events"] = new JsonArray() });
            Write("scenarios.json", new JsonObject { ["scenarios"] = new JsonArray() });
            Write("people.json", new JsonObject { ["people"] = new JsonArray(new JsonObject { ["id"] = "current-person" }) });
            Write("devices.json", new JsonObject { ["devices"] = new JsonArray() });
            Write("locations.json", new JsonObject { ["locations"] = new JsonArray() });
            Write("state.json", new JsonObject { ["schemaVersion"] = "1.0", ["revision"] = 0 });
            Store = DataStore.FromConfig(new AppConfig { NetworkRoot = _root, DataFolder = "data", BackupFolder = "backups", LogsFolder = "logs", BackupRetentionMode = retentionMode });
        }

        internal DataStore Store { get; }
        internal string BackupsRoot => Path.Combine(_root, "backups", "spatial-git");
        internal string LogsRoot => Path.Combine(_root, "logs");
        internal Func<IReadOnlyDictionary<string, string>> DataHashes => () => Directory.EnumerateFiles(_data, "*.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(path => Path.GetFileName(path), path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);
        internal Func<int> BackupCountOnDisk => () => Directory.EnumerateFiles(BackupsRoot, "*.zip").Count() + Directory.EnumerateDirectories(BackupsRoot).Count();

        internal string SingleZipPath() => Directory.EnumerateFiles(BackupsRoot, "*.zip").Single();
        internal JsonObject[] AuditEntries() => Directory.EnumerateFiles(LogsRoot, "audit-*.log")
            .SelectMany(path => File.ReadLines(path))
            .Select(line => JsonNode.Parse(line)?.AsObject() ?? throw new InvalidOperationException("Audit entry is invalid JSON."))
            .ToArray();

        internal void CreateZipBackup(string id, string? createdAtUtc = null, string assignmentPerson = "archived", string peopleId = "archived-person", JsonObject? manifest = null)
        {
            var path = Path.Combine(BackupsRoot, id + ".zip");
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            var backupManifest = manifest ?? Manifest(id, createdAtUtc: createdAtUtc);
            foreach (var (file, document) in ArchiveDocuments(assignmentPerson, peopleId)) WriteArchiveJson(archive, file, document);
            WriteArchiveJson(archive, "state.origin.json", new JsonObject { ["revision"] = 0 });
            WriteArchiveJson(archive, "manifest.json", backupManifest);
        }

        internal void CreateZipBackupWithUnsupportedFile(string id)
        {
            var manifest = Manifest(id, createdAtUtc: "2026-02-02T00:00:00+00:00");
            manifest["files"]!.AsArray().Add("untrusted.json");
            CreateZipBackup(id, manifest: manifest);
        }

        internal void CreateLegacyDirectoryBackup(string id, string createdAt, string assignmentPerson = "legacy", string mapName = "Norte heredado")
        {
            var path = Path.Combine(BackupsRoot, id);
            Directory.CreateDirectory(path);
            Write(Path.Combine("..", "backups", "spatial-git", id, "manifest.json"), new JsonObject { ["id"] = id, ["createdAt"] = createdAt });
            Write(Path.Combine("..", "backups", "spatial-git", id, "maps.json"), Maps(mapName));
            Write(Path.Combine("..", "backups", "spatial-git", id, "assignments.json"), Assignments(assignmentPerson));
            Write(Path.Combine("..", "backups", "spatial-git", id, "positions.json"), Positions());
        }

        internal string AssignmentPerson() => Read("assignments.json")["assignments"]!.AsArray().OfType<JsonObject>().Single()["personId"]!.GetValue<string>();
        internal string PeopleId() => Read("people.json")["people"]!.AsArray().OfType<JsonObject>().Single()["id"]!.GetValue<string>();
        internal string MapName() => Read("maps.json")["maps"]!.AsArray().OfType<JsonObject>().Single()["name"]!.GetValue<string>();

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private Dictionary<string, JsonObject> ArchiveDocuments(string assignmentPerson, string peopleId) => new(StringComparer.OrdinalIgnoreCase)
        {
            ["maps.json"] = Maps("Norte archivado"),
            ["assignments.json"] = Assignments(assignmentPerson),
            ["positions.json"] = Positions(),
            ["events.json"] = new JsonObject { ["events"] = new JsonArray() },
            ["scenarios.json"] = new JsonObject { ["scenarios"] = new JsonArray() },
            ["people.json"] = new JsonObject { ["people"] = new JsonArray(new JsonObject { ["id"] = peopleId }) },
            ["devices.json"] = new JsonObject { ["devices"] = new JsonArray() },
            ["locations.json"] = new JsonObject { ["locations"] = new JsonArray() },
            ["managed-areas.json"] = new JsonObject { ["areas"] = new JsonArray() }
        };

        private JsonObject Manifest(string id, string? createdAtUtc) => new()
        {
            ["id"] = id,
            ["title"] = "Backup",
            ["description"] = "Caracterización",
            ["files"] = new JsonArray(OperationalFiles.Select(file => JsonValue.Create(file)).ToArray()),
            ["transactionFiles"] = new JsonArray("maps.json", "assignments.json", "positions.json", "events.json"),
            ["sourceRevision"] = 0,
            ["createdAt"] = createdAtUtc ?? "2026-02-02T00:00:00+00:00",
            ["createdAtUtc"] = createdAtUtc ?? "2026-02-02T00:00:00+00:00",
            ["createdBy"] = "test"
        };

        private JsonObject Read(string file) => JsonNode.Parse(File.ReadAllText(Path.Combine(_data, file)))?.AsObject()
            ?? throw new InvalidOperationException($"{file} is invalid JSON.");

        private void Write(string file, JsonObject document) => File.WriteAllText(Path.Combine(_data, file), document.ToJsonString());

        private static JsonObject Maps(string name) => new()
        {
            ["maps"] = new JsonArray(new JsonObject
            {
                ["id"] = "north",
                ["name"] = name,
                ["seats"] = new JsonArray(new JsonObject { ["id"] = "N-1", ["x"] = .2, ["y"] = .3 })
            })
        };

        private static JsonObject Assignments(string personId) => new()
        {
            ["assignments"] = new JsonArray(new JsonObject { ["workstationId"] = "N-1", ["personId"] = personId, ["status"] = "confirmed" })
        };

        private static JsonObject Positions() => new()
        {
            ["positions"] = new JsonArray(new JsonObject { ["mapId"] = "north", ["seatId"] = "N-1", ["x"] = .2, ["y"] = .3 })
        };

        private static void WriteArchiveJson(ZipArchive archive, string file, JsonObject document)
        {
            var entry = archive.CreateEntry(file, CompressionLevel.Optimal);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream);
            writer.Write(document.ToJsonString());
        }
    }

    private static JsonObject ReadArchiveJson(ZipArchive archive, string file)
    {
        var entry = archive.GetEntry(file) ?? throw new InvalidOperationException($"Backup lacks {file}.");
        using var stream = entry.Open();
        return JsonNode.Parse(stream)?.AsObject() ?? throw new InvalidOperationException($"Backup {file} is invalid JSON.");
    }

    private static string Text(JsonNode? value) => value?.ToString() ?? "";
}
