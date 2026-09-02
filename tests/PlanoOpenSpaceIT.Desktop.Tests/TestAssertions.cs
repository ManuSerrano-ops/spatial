using System.Text.Json.Nodes;
using Xunit;

namespace PlanoOpenSpaceIT.Desktop.Tests;

internal static class TestAssertions
{
    internal static void EqualHashes(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual,
        string message)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (name, hash) in expected)
        {
            Assert.True(actual.TryGetValue(name, out var result), $"{message} Missing {name}.");
            Assert.Equal(hash, result);
        }
    }

    internal static void EqualJson(JsonNode expected, JsonNode actual, string message) =>
        Assert.True(JsonNode.DeepEquals(expected, actual), message);

    internal static void EqualDocumentContent(JsonObject expected, JsonObject actual, string message)
    {
        var expectedContent = (JsonObject)expected.DeepClone();
        var actualContent = (JsonObject)actual.DeepClone();
        expectedContent.Remove("stateRevision");
        actualContent.Remove("stateRevision");
        EqualJson(expectedContent, actualContent, message);
    }

    internal static void AssertRejectedWithoutWrites(
        Func<IReadOnlyDictionary<string, string>> dataHashes,
        Func<int> backupCount,
        Action action,
        string expectedMessage)
    {
        var hashes = dataHashes();
        var backups = backupCount();
        var error = Record.Exception(action)?.Message ?? "Operation did not fail.";
        Assert.Contains(expectedMessage, error, StringComparison.OrdinalIgnoreCase);
        EqualHashes(hashes, dataHashes(), "A rejected operation is transactionally atomic.");
        Assert.Equal(backups, backupCount());
    }
}
