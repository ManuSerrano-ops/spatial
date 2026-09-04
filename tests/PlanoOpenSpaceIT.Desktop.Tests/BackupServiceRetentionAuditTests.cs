using System.Text.Json.Nodes;
using PlanoOpenSpaceIT.Windows;
using Xunit;

namespace PlanoOpenSpaceIT.Desktop.Tests;

public sealed class BackupServiceRetentionAuditTests
{
    [Fact]
    public void FailedRetentionReportRemovesPathAndEmitsOriginalFailureAuditFields()
    {
        using var fixture = new AuditFixture();
        var document = new JsonObject { ["retentionMode"] = "report" };

        fixture.Service.WriteRetentionReport(document, totalBackups: 3, reclaimableBytes: 42);

        Assert.False(document.ContainsKey("reportPath"));
        Assert.Empty(fixture.InformationAudits);
        var (audit, exception) = Assert.Single(fixture.ErrorAudits);
        Assert.Equal("backup.retention.report.failed", audit.Action);
        Assert.Equal(3, audit.Count);
        Assert.Equal("report", audit.BackupOutcome);
        Assert.Null(audit.ReportPath);
        Assert.IsType<IOException>(exception);
    }

    private sealed class AuditFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "backup-retention-audit-" + Guid.NewGuid().ToString("N"));

        internal AuditFixture()
        {
            Directory.CreateDirectory(_root);
            Service = new BackupService(new BackupService.Storage(
                OperationalFiles: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                TransactionFiles: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                BackupsRoot: Path.Combine(_root, "backups"),
                StatePath: Path.Combine(_root, "state.json"),
                DataPath: file => Path.Combine(_root, "data", file),
                CurrentRevision: () => 0,
                LogInvalidManifest: (_, _) => { },
                LogsRoot: Path.Combine(_root, "logs"),
                CreateDirectory: path => { Directory.CreateDirectory(path); },
                WriteText: (_, _) => throw new IOException("simulated retention report failure"),
                LogRetentionInfo: InformationAudits.Add,
                LogRetentionError: (audit, exception) => ErrorAudits.Add((audit, exception))));
        }

        internal BackupService Service { get; }
        internal List<BackupService.RetentionReportAudit> InformationAudits { get; } = [];
        internal List<(BackupService.RetentionReportAudit Audit, Exception Exception)> ErrorAudits { get; } = [];

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
