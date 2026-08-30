using System.IO;

using System.Text.Json.Nodes;

namespace PlanoOpenSpaceIT.Windows;

internal enum BackupRetentionMode
{
    Disabled,
    Report,
    Delete
}

internal sealed record BackupRetentionCandidate(string Id, DateTimeOffset CreatedAtUtc, long SizeBytes, bool IsLegacy, bool IsProtected);
internal sealed record BackupRetentionDecision(string Id, bool Retain, string Reason, long SizeBytes);

internal static class BackupRetention
{
    internal static BackupRetentionMode ParseMode(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "" or null or "disabled" => BackupRetentionMode.Disabled,
        "report" => BackupRetentionMode.Report,
        "delete" => BackupRetentionMode.Delete,
        _ => throw new InvalidDataException("backupRetentionMode debe ser disabled, report o delete.")
    };

    internal static HashSet<string> ProtectedBackupIds(JsonArray? events)
    {
        return events?.OfType<JsonObject>().Reverse().Take(50)
            .Where(item => string.IsNullOrWhiteSpace(item["undoneAt"]?.ToString()))
            .Select(item => item["backupId"]?.ToString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal) ?? [];
    }

    internal static IReadOnlyList<BackupRetentionDecision> Classify(IEnumerable<BackupRetentionCandidate> source, DateTimeOffset nowUtc)
    {
        var backups = source.OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id, StringComparer.Ordinal).ToArray();
        var latest = backups.FirstOrDefault()?.Id;
        var recent = nowUtc.AddDays(-7);
        var daily = nowUtc.AddDays(-37);
        var monthly = nowUtc.AddMonths(-12);
        var lastDaily = backups.Where(item => item.CreatedAtUtc >= daily && item.CreatedAtUtc < recent)
            .GroupBy(item => item.CreatedAtUtc.UtcDateTime.Date).Select(group => group.First().Id).ToHashSet(StringComparer.Ordinal);
        var lastMonthly = backups.Where(item => item.CreatedAtUtc >= monthly && item.CreatedAtUtc < daily)
            .GroupBy(item => new { item.CreatedAtUtc.Year, item.CreatedAtUtc.Month }).Select(group => group.First().Id).ToHashSet(StringComparer.Ordinal);

        return backups.Select(item =>
        {
            if (item.IsLegacy) return new BackupRetentionDecision(item.Id, true, "legacy", item.SizeBytes);
            if (item.IsProtected) return new BackupRetentionDecision(item.Id, true, "undo-protected", item.SizeBytes);
            if (item.Id == latest) return new BackupRetentionDecision(item.Id, true, "latest", item.SizeBytes);
            if (item.CreatedAtUtc >= recent) return new BackupRetentionDecision(item.Id, true, "recent", item.SizeBytes);
            if (lastDaily.Contains(item.Id)) return new BackupRetentionDecision(item.Id, true, "daily-last", item.SizeBytes);
            if (lastMonthly.Contains(item.Id)) return new BackupRetentionDecision(item.Id, true, "monthly-last", item.SizeBytes);
            return new BackupRetentionDecision(item.Id, false, "expired", item.SizeBytes);
        }).ToArray();
    }
}
