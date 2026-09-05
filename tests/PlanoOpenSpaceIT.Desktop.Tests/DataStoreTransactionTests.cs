using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using PlanoOpenSpaceIT.Windows;
using Xunit;

namespace PlanoOpenSpaceIT.Desktop.Tests;

public sealed class DataStoreTransactionTests
{
    // Do not call DataStore again between the action under test and filesystem assertions:
    // every public operation enters WithLock and can recover or clean protocol residue,
    // hiding the effect being tested.
    private static readonly string[] TransactionFiles = ["maps.json", "assignments.json", "positions.json", "events.json"];

    [Fact]
    public void SuccessfulCommitPublishesAllDocumentsAtOneRevision()
    {
        using var fixture = new TransactionFixture();

        var backupId = fixture.SaveAssignment("person-2");

        Assert.Equal(1, fixture.StateRevision());
        Assert.Equal("person-2", fixture.AssignmentPerson("S-0002"));
        Assert.All(TransactionFiles, file => Assert.Equal(1, fixture.DocumentRevision(file)));
        Assert.Single(fixture.Events());
        Assert.Equal(backupId, fixture.LatestBackupId());
        Assert.Equal(TransactionFiles.Order(StringComparer.Ordinal), fixture.BackupTransactionFiles(backupId).Order(StringComparer.Ordinal));
        fixture.AssertNoProtocolResidue();
    }

    [Fact]
    public void CommitCompletesWhenAuditLogIsUnavailable()
    {
        using var fixture = new TransactionFixture(blockLogs: true);

        var backupId = fixture.SaveAssignment("person-2");

        Assert.Equal([AuditLogAvailability.Unavailable], fixture.AuditLogAvailability);
        Assert.Equal(1, fixture.StateRevision());
        Assert.Equal("person-2", fixture.AssignmentPerson("S-0002"));
        Assert.All(TransactionFiles, file => Assert.Equal(1, fixture.DocumentRevision(file)));
        Assert.Single(fixture.Events());
        Assert.Equal(backupId, fixture.LatestBackupId());
        fixture.AssertNoProtocolResidue();
    }

    [Fact]
    public void RejectedMutationLeavesNoPublishedTransactionEffects()
    {
        using var fixture = new TransactionFixture();

        TestAssertions.AssertRejectedWithoutWrites(
            fixture.DataHashes,
            fixture.BackupCount,
            () => fixture.Store.BulkUpdateAssignments(new JsonObject { ["workstationIds"] = new JsonArray("S-0001"), ["status"] = "reserved" }),
            "ocupado");

        Assert.Equal(0, fixture.StateRevision());
        Assert.Empty(fixture.Events());
        fixture.AssertNoProtocolResidue();
    }

    [Fact]
    public void ConfirmedPendingTransactionFinishesProtocolCleanupBeforeLoad()
    {
        using var fixture = new TransactionFixture();
        _ = fixture.SaveAssignment("person-1");
        var backupId = fixture.SaveAssignment("person-2");
        fixture.WriteStateRevision(1);
        var transactionId = fixture.CreatePending(backupId, sourceRevision: 1, destinationRevision: 2);
        fixture.CreateTemporary("assignments.json", transactionId);

        _ = fixture.Store.Load();

        Assert.Equal("person-2", fixture.AssignmentPerson("S-0002"));
        Assert.Equal(2, fixture.StateRevision());
        Assert.All(TransactionFiles, file => Assert.Equal(2, fixture.DocumentRevision(file)));
        fixture.AssertNoProtocolResidue();
    }

    [Fact]
    public void UnpublishedPendingTransactionIsDiscardedBeforeLoad()
    {
        using var fixture = new TransactionFixture();
        _ = fixture.SaveAssignment("person-1");
        var backupId = fixture.SaveAssignment("person-2");
        fixture.RestoreTransactionFilesFromBackup(backupId);
        fixture.WriteStateRevision(1);
        var transactionId = fixture.CreatePending(backupId, sourceRevision: 1, destinationRevision: 2);
        fixture.CreateTemporary("positions.json", transactionId);

        _ = fixture.Store.Load();

        Assert.Equal("person-1", fixture.AssignmentPerson("S-0002"));
        Assert.Equal(1, fixture.StateRevision());
        Assert.All(TransactionFiles, file => Assert.Equal(1, fixture.DocumentRevision(file)));
        fixture.AssertNoProtocolResidue();
    }

    [Fact]
    public void PartiallyPublishedPendingTransactionRestoresWholeBackup()
    {
        using var fixture = new TransactionFixture();
        _ = fixture.SaveAssignment("person-1");
        var backupId = fixture.SaveAssignment("person-2");
        fixture.RestoreTransactionFilesFromBackup(backupId, "assignments.json");
        fixture.WriteStateRevision(1);
        var transactionId = fixture.CreatePending(backupId, sourceRevision: 1, destinationRevision: 2);
        fixture.CreateTemporary("assignments.json", transactionId);

        _ = fixture.Store.Load();

        Assert.Equal("person-1", fixture.AssignmentPerson("S-0002"));
        Assert.Equal(2, fixture.StateRevision());
        Assert.All(TransactionFiles, file => Assert.Equal(2, fixture.DocumentRevision(file)));
        Assert.Contains(fixture.Events(), item => string.Equals(item["recovery"]?.GetValue<string>(), "reverted", StringComparison.Ordinal));
        fixture.AssertNoProtocolResidue();
    }

    [Fact]
    public void DestinationStateWithUnpublishedTemporariesRevertsWholeBackup()
    {
        using var fixture = new TransactionFixture();
        _ = fixture.SaveAssignment("person-1");
        var backupId = fixture.SaveAssignment("person-2");
        var destinationDocuments = fixture.TransactionDocumentContents();
        fixture.RestoreTransactionFilesFromBackup(backupId);
        fixture.WriteStateRevision(2);
        var transactionId = fixture.CreatePending(backupId, sourceRevision: 1, destinationRevision: 2);
        foreach (var file in TransactionFiles) fixture.CreateTemporary(file, transactionId, destinationDocuments[file]);

        _ = fixture.Store.Load();

        Assert.Equal("person-1", fixture.AssignmentPerson("S-0002"));
        Assert.Equal(2, fixture.StateRevision());
        Assert.All(TransactionFiles, file => Assert.Equal(2, fixture.DocumentRevision(file)));
        Assert.Contains(fixture.Events(), item => string.Equals(item["recovery"]?.GetValue<string>(), "reverted", StringComparison.Ordinal));
        fixture.AssertNoProtocolResidue();
    }

    [Fact]
    public void RealCommitObserverIsAliveAndNeverSeesDestinationStateWithTransactionTemporaries()
    {
        using var fixture = new TransactionFixture(seatCount: 2_000);
        using var helper = StartCommitThenDie(fixture.Root);

        Assert.True(helper.WaitForExit(30_000), "The commit helper did not finish within 30 seconds.");
        var output = helper.StandardOutput.ReadToEnd();
        var errorOutput = helper.StandardError.ReadToEnd();
        var windowObserved = fixture.ObserverWindowExists;
        var aliveObserved = fixture.ObserverAliveExists;

        if (windowObserved)
        {
            _ = fixture.Store.Load();
            Assert.Equal(1, fixture.StateRevision());
            Assert.All(TransactionFiles, file => Assert.Equal(1, fixture.DocumentRevision(file)));
            fixture.AssertNoProtocolResidue();
        }

        Assert.True(aliveObserved, "The observer must prove it watched pending or temporary transaction files.");
        Assert.Contains("ALIVE", output, StringComparison.Ordinal);
        Assert.False(windowObserved, "The writer must not expose destination state while transaction temporaries remain unpublished.");
        Assert.True(fixture.ObserverCommittedExists, $"A correct-order commit must complete normally. Exit={helper.ExitCode}; stdout={output}; stderr={errorOutput}");
        Assert.Equal(0, helper.ExitCode);
        Assert.Contains("COMMITTED", output, StringComparison.Ordinal);
        Assert.Equal("person-committed", fixture.AssignmentPerson("S-0002"));
        Assert.Equal(1, fixture.StateRevision());
        Assert.All(TransactionFiles, file => Assert.Equal(1, fixture.DocumentRevision(file)));
        fixture.AssertNoProtocolResidue();
    }

    [Fact]
    public void PendingTransactionWithoutBackupBlocksLoadWithoutChangingFiles()
    {
        using var fixture = new TransactionFixture();
        _ = fixture.CreatePending("20260101000000000-a1b2c3", sourceRevision: 0, destinationRevision: 1);
        var before = fixture.DataHashes();

        var error = Capture(() => fixture.Store.Load());

        Assert.Contains("No se pudo recuperar la transacción pendiente", error, StringComparison.Ordinal);
        TestAssertions.EqualHashes(before, fixture.DataHashes(), "An unrecoverable pending transaction must remain untouched for manual intervention.");
        Assert.True(fixture.PendingExists);
    }

    [Fact]
    public void FailedOperationReleasesLockForFollowingCommit()
    {
        using var fixture = new TransactionFixture();

        _ = Capture(() => fixture.Store.BulkUpdateAssignments(new JsonObject { ["workstationIds"] = new JsonArray("S-0001"), ["status"] = "reserved" }));
        _ = fixture.SaveAssignment("person-2");

        Assert.Equal("person-2", fixture.AssignmentPerson("S-0002"));
        Assert.Equal(1, fixture.StateRevision());
        fixture.AssertNoProtocolResidue();
    }

    [Fact]
    public void LargeDatasetCommitPublishesAllDocumentsAtOneRevision()
    {
        using var fixture = new TransactionFixture(seatCount: 2_000);

        _ = fixture.SaveAssignment("person-updated", "S-2000");

        Assert.Equal("person-updated", fixture.AssignmentPerson("S-2000"));
        Assert.Equal(1, fixture.StateRevision());
        Assert.All(TransactionFiles, file => Assert.Equal(1, fixture.DocumentRevision(file)));
        fixture.AssertNoProtocolResidue();
    }

    private static string Capture(Action action)
    {
        try { action(); }
        catch (Exception exception) { return exception.Message; }
        throw new InvalidOperationException("Expected operation to fail.");
    }

    private static Process StartCommitThenDie(string root)
    {
        var harness = Assembly.GetExecutingAssembly().Location;
        return Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{harness}\" --commit-then-die \"{root}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Could not start the commit observer helper.");
    }

    private sealed class TransactionFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "datastore-transaction-" + Guid.NewGuid().ToString("N"));
        private readonly string _data;

        internal TransactionFixture(int seatCount = 2, bool blockLogs = false)
        {
            _data = Path.Combine(_root, "data");
            Directory.CreateDirectory(_data);
            Write("maps.json", Maps(seatCount));
            Write("assignments.json", Assignments(seatCount));
            Write("positions.json", Positions(seatCount));
            Write("events.json", new JsonObject { ["events"] = new JsonArray() });
            Write("people.json", new JsonObject { ["people"] = new JsonArray() });
            Write("devices.json", new JsonObject { ["devices"] = new JsonArray() });
            Write("locations.json", new JsonObject { ["locations"] = new JsonArray() });
            Write("state.json", new JsonObject { ["revision"] = 0 });
            if (blockLogs) File.WriteAllText(Path.Combine(_root, "logs"), "blocked");
            Store = DataStore.FromConfig(
                new AppConfig { NetworkRoot = _root, DataFolder = "data", BackupFolder = "backups", LogsFolder = "logs", BackupRetentionMode = "disabled" },
                auditLogAvailabilityChanged: AuditLogAvailability.Add);
        }

        internal DataStore Store { get; }
        internal List<AuditLogAvailability> AuditLogAvailability { get; } = [];
        internal string Root => _root;
        internal bool PendingExists => File.Exists(Path.Combine(_data, "commit.pending"));
        internal bool ObserverAliveExists => File.Exists(Path.Combine(_root, "observer.alive"));
        internal bool ObserverWindowExists => File.Exists(Path.Combine(_root, "observer.window"));
        internal bool ObserverCommittedExists => File.Exists(Path.Combine(_root, "observer.committed"));

        internal string SaveAssignment(string personId, string workstationId = "S-0002")
        {
            Store.SaveAssignment(new JsonObject { ["workstationId"] = workstationId, ["personId"] = personId, ["status"] = "confirmed" }, false);
            return LatestBackupId();
        }

        internal string LatestBackupId() => Directory.EnumerateFiles(BackupsRoot, "*.zip")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal)
            .LastOrDefault()
            ?? throw new InvalidOperationException("Expected transaction backup.");

        internal int BackupCount() => Directory.Exists(BackupsRoot) ? Directory.EnumerateFiles(BackupsRoot, "*.zip").Count() : 0;

        internal IReadOnlyDictionary<string, string> DataHashes() => Directory.EnumerateFiles(_data, "*.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(path => Path.GetFileName(path), path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);

        internal long StateRevision() => Read("state.json")["revision"]?.GetValue<long>() ?? throw new InvalidOperationException("state.json has no revision.");

        internal long DocumentRevision(string file) => Read(file)["stateRevision"]?.GetValue<long>() ?? throw new InvalidOperationException($"{file} has no stateRevision.");

        internal string AssignmentPerson(string workstationId) => Read("assignments.json")["assignments"]?.AsArray().OfType<JsonObject>()
            .Single(item => string.Equals(item["workstationId"]?.GetValue<string>(), workstationId, StringComparison.Ordinal))["personId"]?.GetValue<string>()
            ?? throw new InvalidOperationException($"Assignment {workstationId} has no person.");

        internal JsonObject[] Events() => Read("events.json")["events"]?.AsArray().OfType<JsonObject>().ToArray() ?? [];

        internal IReadOnlyDictionary<string, string> TransactionDocumentContents() => TransactionFiles
            .ToDictionary(file => file, file => File.ReadAllText(Path.Combine(_data, file)), StringComparer.Ordinal);

        internal string[] BackupTransactionFiles(string backupId)
        {
            using var archive = ZipFile.OpenRead(BackupPath(backupId));
            var manifest = ReadArchiveJson(archive, "manifest.json");
            return manifest["transactionFiles"]?.AsArray().Select(item => item?.GetValue<string>() ?? "").ToArray() ?? [];
        }

        internal void RestoreTransactionFilesFromBackup(string backupId, params string[] files)
        {
            var selected = files.Length == 0 ? TransactionFiles : files;
            using var archive = ZipFile.OpenRead(BackupPath(backupId));
            foreach (var file in selected)
            {
                var entry = archive.GetEntry(file) ?? throw new InvalidOperationException($"Backup lacks {file}.");
                using var source = entry.Open();
                using var destination = File.Create(Path.Combine(_data, file));
                source.CopyTo(destination);
            }
        }

        internal void WriteStateRevision(long revision) => Write("state.json", new JsonObject { ["schemaVersion"] = "1.0", ["revision"] = revision });

        internal string CreatePending(string backupId, long sourceRevision, long destinationRevision)
        {
            var transactionId = Guid.NewGuid().ToString("N");
            Write("commit.pending", new JsonObject
            {
                ["schemaVersion"] = "1.0",
                ["transactionId"] = transactionId,
                ["backupId"] = backupId,
                ["sourceRevision"] = sourceRevision,
                ["destinationRevision"] = destinationRevision,
                ["files"] = new JsonArray(TransactionFiles.Select(file => (JsonNode?)file).ToArray()),
                ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"),
                ["createdBy"] = "test"
            });
            return transactionId;
        }

        internal void CreateTemporary(string file, string transactionId, string content = "temporary") => File.WriteAllText(Path.Combine(_data, $"{file}.{transactionId}.tmp"), content);

        internal void AssertNoProtocolResidue()
        {
            Assert.False(PendingExists, "A completed protocol leaves no commit.pending marker.");
            Assert.Empty(Directory.EnumerateFiles(_data, "*.tmp"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private JsonObject Read(string file) => JsonNode.Parse(File.ReadAllText(Path.Combine(_data, file)))?.AsObject()
            ?? throw new InvalidOperationException($"{file} is invalid JSON.");

        private string BackupsRoot => Path.Combine(_root, "backups", "spatial-git");

        private string BackupPath(string backupId) => Path.Combine(BackupsRoot, backupId + ".zip");

        private void Write(string file, JsonObject document) => File.WriteAllText(Path.Combine(_data, file), document.ToJsonString());

        private static JsonObject ReadArchiveJson(ZipArchive archive, string file)
        {
            var entry = archive.GetEntry(file) ?? throw new InvalidOperationException($"Backup lacks {file}.");
            using var stream = entry.Open();
            return JsonNode.Parse(stream)?.AsObject() ?? throw new InvalidOperationException($"Backup {file} is invalid JSON.");
        }

        private static JsonObject Maps(int seatCount) => new()
        {
            ["maps"] = new JsonArray(new JsonObject
            {
                ["id"] = "sur",
                ["name"] = "Sur",
                ["seats"] = new JsonArray(Enumerable.Range(1, seatCount).Select(index => (JsonNode?)new JsonObject
                {
                    ["id"] = $"S-{index:D4}",
                    ["x"] = (index % 40) / 40d,
                    ["y"] = (index % 50) / 50d
                }).ToArray())
            })
        };

        private static JsonObject Assignments(int seatCount) => new()
        {
            ["version"] = 0,
            ["assignments"] = new JsonArray(Enumerable.Range(1, seatCount).Select(index => (JsonNode?)new JsonObject
            {
                ["workstationId"] = $"S-{index:D4}",
                ["personId"] = $"person-{index}",
                ["status"] = "confirmed"
            }).ToArray())
        };

        private static JsonObject Positions(int seatCount) => new()
        {
            ["positions"] = new JsonArray(Enumerable.Range(1, seatCount).Select(index => (JsonNode?)new JsonObject
            {
                ["mapId"] = "sur",
                ["seatId"] = $"S-{index:D4}",
                ["x"] = (index % 40) / 40d,
                ["y"] = (index % 50) / 50d
            }).ToArray())
        };
    }
}
