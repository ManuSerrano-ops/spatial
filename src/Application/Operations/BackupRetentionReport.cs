using System.IO;
using System.Text.Json.Nodes;

namespace PlanoOpenSpaceIT.Windows;

internal sealed record BackupRetentionReportEntry(
    string Id,
    DateTimeOffset CreatedAtUtc,
    long SizeBytes,
    bool IsLegacy,
    bool IsProtected,
    bool Retain,
    string Reason);

internal sealed record BackupRetentionReportData(
    int TotalBackups,
    long TotalBytes,
    DateTimeOffset? OldestCreatedAtUtc,
    int UndoProtectedCount,
    long ReclaimableBytes,
    IReadOnlyList<BackupRetentionReportEntry> Backups);

internal static class BackupRetentionReport
{
    internal static BackupRetentionReportData Build(
        IEnumerable<(string Id, string Container, JsonObject Manifest, bool Legacy)> containers,
        JsonArray? events,
        DateTimeOffset nowUtc)
    {
        var protectedIds = BackupRetention.ProtectedBackupIds(events);
        var candidates = containers.Select(item => new BackupRetentionCandidate(
            item.Id,
            DataStore.BackupCreatedAtUtc(item.Manifest) ?? DateTimeOffset.MinValue,
            Size(item.Container),
            item.Legacy,
            protectedIds.Contains(item.Id))).ToArray();
        var decisions = BackupRetention.Classify(candidates, nowUtc).ToDictionary(item => item.Id, StringComparer.Ordinal);
        var backups = candidates
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id, StringComparer.Ordinal)
            .Select(item =>
            {
                var decision = decisions[item.Id];
                return new BackupRetentionReportEntry(item.Id, item.CreatedAtUtc, item.SizeBytes, item.IsLegacy, item.IsProtected, decision.Retain, decision.Reason);
            })
            .ToArray();

        return new BackupRetentionReportData(
            candidates.Length,
            candidates.Sum(item => item.SizeBytes),
            candidates.Length == 0 ? null : candidates.Min(item => item.CreatedAtUtc),
            candidates.Count(item => item.IsProtected),
            backups.Where(item => !item.Retain).Sum(item => item.SizeBytes),
            backups);
    }

    private static long Size(string path)
    {
        if (File.Exists(path)) return new FileInfo(path).Length;
        return Directory.Exists(path) ? Directory.GetFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length) : 0;
    }
}
