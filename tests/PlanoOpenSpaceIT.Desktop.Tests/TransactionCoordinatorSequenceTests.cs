using System.Text.Json.Nodes;
using PlanoOpenSpaceIT.Windows;
using Xunit;

namespace PlanoOpenSpaceIT.Desktop.Tests;

public sealed class TransactionCoordinatorSequenceTests
{
    [Fact]
    public void CommitPublishesProtocolMarkersInOrder()
    {
        using var fixture = new SequenceFixture();

        _ = fixture.Coordinator.Execute(
            new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase)
            {
                ["assignments.json"] = new JsonObject { ["assignments"] = new JsonArray() }
            },
            ["assignments.json"],
            "Antes de probar el protocolo",
            "Protocolo probado",
            "Secuencia de commit",
            sourceRevision: 0);

        fixture.AssertOrder(
            "pending.write",
            "temporary.publish",
            "state.write",
            "pending.delete");
    }

    private sealed class SequenceFixture : IDisposable
    {
        private const string BackupId = "20260101000000000-a1b2c3";
        private readonly string _root = Path.Combine(Path.GetTempPath(), "transaction-sequence-" + Guid.NewGuid().ToString("N"));
        private readonly string _data;
        private readonly List<string> _operations = [];

        internal SequenceFixture()
        {
            _data = Path.Combine(_root, "data");
            Directory.CreateDirectory(_data);
            WriteDocument("state.json", new JsonObject { ["revision"] = 0 });
            WriteDocument("assignments.json", new JsonObject { ["assignments"] = new JsonArray() });
            WriteDocument("events.json", new JsonObject { ["events"] = new JsonArray() });
            Coordinator = new TransactionCoordinator(new TransactionCoordinator.Storage(
                new HashSet<string>(["assignments.json", "events.json"], StringComparer.OrdinalIgnoreCase),
                ReadOnly: false,
                DataPath,
                PendingPath,
                CurrentRevision,
                WriteState,
                ReadDocument,
                WriteAtomic,
                WriteText,
                Move,
                Delete,
                File.Exists,
                Directory.Exists,
                (_, _) => BackupId,
                _ => Path.Combine(_root, "backups", "spatial-git", BackupId + ".zip"),
                _ => true,
                (_, _) => true,
                (_, _) => new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase),
                id => string.Equals(id, BackupId, StringComparison.Ordinal),
                _ => { },
                (_, _) => { }));
        }

        internal TransactionCoordinator Coordinator { get; }

        internal void AssertOrder(params string[] expected)
        {
            var positions = expected.ToDictionary(operation => operation, operation => _operations.IndexOf(operation), StringComparer.Ordinal);
            Assert.All(positions, pair => Assert.True(pair.Value >= 0, $"No se observó {pair.Key}. Operaciones: {string.Join(", ", _operations)}"));
            for (var index = 1; index < expected.Length; index++)
            {
                Assert.True(
                    positions[expected[index - 1]] < positions[expected[index]],
                    $"{expected[index - 1]} debe ocurrir antes de {expected[index]}. Operaciones: {string.Join(", ", _operations)}");
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private string PendingPath => Path.Combine(_data, "commit.pending");

        private string DataPath(string file) => Path.Combine(_data, file);

        private long CurrentRevision() => ReadDocument(DataPath("state.json"))?["revision"]?.GetValue<long>()
            ?? throw new InvalidOperationException("state.json no contiene revisión.");

        private void WriteState(long revision)
        {
            _operations.Add("state.write");
            WriteDocument("state.json", new JsonObject { ["revision"] = revision });
        }

        private JsonObject? ReadDocument(string path) => File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            : null;

        private void WriteAtomic(string path, JsonObject document)
        {
            if (string.Equals(path, PendingPath, StringComparison.Ordinal)) _operations.Add("pending.write");
            WriteDocument(Path.GetFileName(path), document);
        }

        private void WriteText(string path, string content)
        {
            _operations.Add("temporary.write");
            File.WriteAllText(path, content);
        }

        private void Move(string source, string destination)
        {
            _operations.Add("temporary.publish");
            File.Move(source, destination, true);
        }

        private void Delete(string path)
        {
            if (string.Equals(path, PendingPath, StringComparison.Ordinal)) _operations.Add("pending.delete");
            File.Delete(path);
        }

        private void WriteDocument(string file, JsonObject document) => File.WriteAllText(Path.Combine(_data, file), document.ToJsonString());
    }
}
