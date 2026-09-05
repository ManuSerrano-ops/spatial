using System.IO;
using System.Text;
using System.Text.Json;

namespace PlanoOpenSpaceIT.Windows;

internal enum AuditLogAvailability
{
    Unavailable,
    Available
}

internal sealed class SafeLogger
{
    internal const long DefaultMaxFileSizeBytes = 1_048_576;
    internal const int DefaultMaxHistoryFiles = 5;
    private const long MinimumMaxFileSizeBytes = 1_024;
    private const int MaximumStackLength = 8_192;
    private readonly string _folder;
    private readonly long _maxFileSizeBytes;
    private readonly int _maxHistoryFiles;
    private readonly Action<AuditLogAvailability> _availabilityChanged;
    private int _unavailable;

    internal SafeLogger(string folder, long maxFileSizeBytes = DefaultMaxFileSizeBytes, int maxHistoryFiles = DefaultMaxHistoryFiles, Action<AuditLogAvailability>? availabilityChanged = null)
    {
        _folder = folder;
        _maxFileSizeBytes = Math.Max(maxFileSizeBytes, MinimumMaxFileSizeBytes);
        _maxHistoryFiles = Math.Max(maxHistoryFiles, 1);
        _availabilityChanged = availabilityChanged ?? WriteAvailabilityToConsole;
        VerifyAvailability();
    }

    internal void Info(string action, string? seatId = null, long? sourceRevision = null, long? destinationRevision = null, string? backupId = null, string? transactionId = null, IEnumerable<string>? files = null, string? backupOutcome = null, int? count = null, string? bridgeAction = null, string? scenarioId = null, string? result = null, long? currentRevision = null, long? durationMs = null, string? applicationBuild = null, string? exportPath = null, string? reportPath = null, IReadOnlyDictionary<string, object?>? details = null)
        => Write(CreateEntry("information", action, seatId, sourceRevision, destinationRevision, backupId, transactionId, files, backupOutcome, count, bridgeAction, scenarioId, result, currentRevision, durationMs, applicationBuild, exportPath, reportPath, null, details));

    internal void Error(string action, Exception exception, string? seatId = null, long? sourceRevision = null, long? destinationRevision = null, string? backupId = null, string? transactionId = null, IEnumerable<string>? files = null, string? backupOutcome = null, int? count = null, string? bridgeAction = null, string? scenarioId = null, string? result = null, long? currentRevision = null, long? durationMs = null, string? applicationBuild = null, string? exportPath = null, string? reportPath = null)
        => Write(CreateEntry("error", action, seatId, sourceRevision, destinationRevision, backupId, transactionId, files, backupOutcome, count, bridgeAction, scenarioId, result, currentRevision, durationMs, applicationBuild, exportPath, reportPath, exception));

    private static Dictionary<string, object?> CreateEntry(string level, string action, string? seatId, long? sourceRevision, long? destinationRevision, string? backupId, string? transactionId, IEnumerable<string>? files, string? backupOutcome, int? count, string? bridgeAction, string? scenarioId, string? result, long? currentRevision, long? durationMs, string? applicationBuild, string? exportPath, string? reportPath, Exception? exception, IReadOnlyDictionary<string, object?>? details = null)
    {
        var entry = new Dictionary<string, object?>
        {
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
            ["level"] = level,
            ["action"] = action
        };
        Add(entry, "seatId", seatId);
        Add(entry, "sourceRevision", sourceRevision);
        Add(entry, "destinationRevision", destinationRevision);
        Add(entry, "backupId", backupId);
        Add(entry, "transactionId", transactionId);
        Add(entry, "files", files?.Select(Path.GetFileName).ToArray());
        Add(entry, "backupOutcome", backupOutcome);
        Add(entry, "count", count);
        Add(entry, "bridgeAction", bridgeAction);
        Add(entry, "scenarioId", scenarioId);
        Add(entry, "result", result);
        Add(entry, "currentRevision", currentRevision);
        Add(entry, "durationMs", durationMs);
        Add(entry, "applicationBuild", applicationBuild);
        Add(entry, "exportFile", string.IsNullOrWhiteSpace(exportPath) ? null : Path.GetFileName(exportPath));
        Add(entry, "reportFile", string.IsNullOrWhiteSpace(reportPath) ? null : Path.GetFileName(reportPath));
        if (details is not null) foreach (var (name, value) in details) Add(entry, name, value);
        if (exception is not null)
        {
            entry["errorType"] = exception.GetType().FullName;
            entry["errorMessage"] = exception.Message;
            entry["stack"] = Truncate(exception.ToString(), MaximumStackLength);
        }
        return entry;
    }

    private void VerifyAvailability()
    {
        try
        {
            Directory.CreateDirectory(_folder);
            var probe = Path.Combine(_folder, $".audit-probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty, Encoding.UTF8);
            File.Delete(probe);
            MarkAvailable();
        }
        catch
        {
            MarkUnavailable();
        }
    }

    private void Write(Dictionary<string, object?> entry)
    {
        try
        {
            Directory.CreateDirectory(_folder);
            var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
            var path = Path.Combine(_folder, $"audit-{Environment.ProcessId}.log");
            if (File.Exists(path) && new FileInfo(path).Length > 0 && new FileInfo(path).Length + Encoding.UTF8.GetByteCount(line) > _maxFileSizeBytes)
            {
                var oldest = path + "." + _maxHistoryFiles;
                if (File.Exists(oldest)) File.Delete(oldest);
                for (var index = _maxHistoryFiles - 1; index >= 1; index--)
                {
                    var source = path + "." + index;
                    if (File.Exists(source)) File.Move(source, path + "." + (index + 1));
                }
                File.Move(path, path + ".1");
            }
            File.AppendAllText(path, line, Encoding.UTF8);
            MarkAvailable();
        }
        catch
        {
            MarkUnavailable();
        }
    }

    private void MarkUnavailable()
    {
        if (Interlocked.Exchange(ref _unavailable, 1) != 0) return;
        NotifyAvailability(AuditLogAvailability.Unavailable);
    }

    private void MarkAvailable()
    {
        if (Interlocked.Exchange(ref _unavailable, 0) == 0) return;
        NotifyAvailability(AuditLogAvailability.Available);
    }

    private void NotifyAvailability(AuditLogAvailability availability)
    {
        try { _availabilityChanged(availability); }
        catch { }
    }

    private static void WriteAvailabilityToConsole(AuditLogAvailability availability)
    {
        if (availability == AuditLogAvailability.Unavailable) Console.Error.WriteLine("PlanoOpenSpaceIT: no se pudo escribir el registro de auditoría.");
    }

    private static void Add(Dictionary<string, object?> entry, string name, object? value)
    {
        if (value is not null) entry[name] = value;
    }

    private static string Truncate(string value, int maximumLength) => value.Length <= maximumLength ? value : value[..maximumLength];
}
