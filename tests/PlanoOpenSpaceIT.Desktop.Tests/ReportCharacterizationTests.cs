using System.Text.Json.Nodes;
using PlanoOpenSpaceIT.Windows;
using Xunit;

namespace PlanoOpenSpaceIT.Desktop.Tests;

public sealed class ReportCharacterizationTests
{
    [Fact]
    public void ValidationAndAnalyticsEmitCurrentAuditContractIncludingAsymmetry()
    {
        using var fixture = new ReportFixture();

        var validation = fixture.Store.RunValidation();
        var analytics = fixture.Store.RunSpatialAnalytics();
        var audit = fixture.AuditEntries();
        var validationStarted = Assert.Single(audit, entry => Text(entry["action"]) == "validation.started");
        var validationFinished = Assert.Single(audit, entry => Text(entry["action"]) == "validation.finished");
        var analyticsFinished = Assert.Single(audit, entry => Text(entry["action"]) == "analytics.finished");

        Assert.Equal("information", Text(validationStarted["level"]));
        Assert.False(validationStarted.ContainsKey("count"));
        Assert.False(validationStarted.ContainsKey("durationMs"));

        Assert.Equal("information", Text(validationFinished["level"]));
        Assert.Equal(validation["count"]?.GetValue<int>(), validationFinished["count"]?.GetValue<int>());
        Assert.True(validationFinished["durationMs"]?.GetValue<long>() >= 0);
        Assert.Equal(validation["summary"]?["critical"]?.GetValue<int>(), validationFinished["critical"]?.GetValue<int>());
        Assert.Equal(validation["summary"]?["warning"]?.GetValue<int>(), validationFinished["warning"]?.GetValue<int>());
        Assert.Equal(validation["summary"]?["info"]?.GetValue<int>(), validationFinished["info"]?.GetValue<int>());

        Assert.Equal("information", Text(analyticsFinished["level"]));
        Assert.Equal(analytics["result"]?["totals"]?["total"]?.GetValue<int>(), analyticsFinished["count"]?.GetValue<int>());
        Assert.True(analyticsFinished["durationMs"]?.GetValue<long>() >= 0);
        Assert.Equal(analytics["result"]?["totals"]?["occupied"]?.GetValue<int>(), analyticsFinished["occupied"]?.GetValue<int>());
        Assert.Equal(analytics["result"]?["totals"]?["free"]?.GetValue<int>(), analyticsFinished["free"]?.GetValue<int>());
        Assert.Equal(analytics["result"]?["totals"]?["reserved"]?.GetValue<int>(), analyticsFinished["reserved"]?.GetValue<int>());
        Assert.Equal(analytics["result"]?["validation"]?["total"]?.GetValue<int>(), analyticsFinished["problems"]?.GetValue<int>());
        Assert.Equal(0, analyticsFinished["scenarioChanges"]?.GetValue<int>());
        Assert.DoesNotContain(audit, entry => Text(entry["action"]) == "analytics.started");
    }

    [Fact]
    public void IntegrityReportPreservesResponseFileAndSuccessAuditContract()
    {
        using var fixture = new ReportFixture();

        var report = fixture.Store.GetIntegrityReport();
        var counts = report["counts"]?.AsObject() ?? throw new InvalidOperationException("Integrity report has no counts.");
        var reportPath = Text(report["reportPath"]);
        var audit = Assert.Single(fixture.AuditEntries(), entry => Text(entry["action"]) == "integrity.report");

        Assert.True(DateTimeOffset.TryParse(Text(report["generatedAtUtc"]), out _));
        Assert.Equal(1, counts["duplicateRosetas"]?.GetValue<int>());
        Assert.Equal(1, counts["historicalOccupiedMarksWithoutAssignment"]?.GetValue<int>());
        Assert.Equal(1, counts["assignmentsWithMissingWorkstation"]?.GetValue<int>());
        Assert.Equal(1, counts["orphanPositions"]?.GetValue<int>());
        Assert.Equal(["N-1"], report["historicalOccupiedMarksWithoutAssignment"]?.AsArray().Select(Text));
        Assert.Equal(["N-missing"], report["assignmentsWithMissingWorkstation"]?.AsArray().Select(Text));
        Assert.Equal("R-duplicate", Text(report["duplicateRosetas"]?[0]?["roseta"]));
        Assert.Equal(["N-2", "N-3"], report["duplicateRosetas"]?[0]?["workstationIds"]?.AsArray().Select(Text));
        Assert.Equal("north", Text(report["orphanPositions"]?[0]?["mapId"]));
        Assert.Equal("N-missing", Text(report["orphanPositions"]?[0]?["seatId"]));
        Assert.True(File.Exists(reportPath), "Integrity report must be persisted under logs.");
        Assert.True(JsonNode.DeepEquals(report, fixture.ReadJsonFile(reportPath)));

        Assert.Equal("information", Text(audit["level"]));
        Assert.Equal(4, audit["count"]?.GetValue<int>());
        Assert.Equal(0, audit["currentRevision"]?.GetValue<long>());
        Assert.Equal(Path.GetFileName(reportPath), Text(audit["reportFile"]));
    }

    [Fact]
    public void IntegrityReportWriteFailureRemovesPathButCannotPersistFailureAuditWhenLogsAreUnavailable()
    {
        using var fixture = new ReportFixture(blockLogs: true);

        var report = fixture.Store.GetIntegrityReport();

        Assert.False(report.ContainsKey("reportPath"));
        Assert.Empty(fixture.AuditEntries());
    }

    private sealed class ReportFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "report-characterization-" + Guid.NewGuid().ToString("N"));
        private readonly string _data;

        internal ReportFixture(bool blockLogs = false)
        {
            _data = Path.Combine(_root, "data");
            Directory.CreateDirectory(_data);
            Write("maps.json", new JsonObject
            {
                ["maps"] = new JsonArray(new JsonObject
                {
                    ["id"] = "north", ["name"] = "North",
                    ["seats"] = new JsonArray(
                        new JsonObject { ["id"] = "N-1", ["type"] = "occupied", ["x"] = .1, ["y"] = .2 },
                        new JsonObject { ["id"] = "N-2", ["type"] = "free", ["x"] = .3, ["y"] = .4 },
                        new JsonObject { ["id"] = "N-3", ["type"] = "free", ["x"] = .5, ["y"] = .6 })
                })
            });
            Write("assignments.json", new JsonObject
            {
                ["assignments"] = new JsonArray(
                    new JsonObject { ["workstationId"] = "N-2", ["status"] = "confirmed", ["roseta"] = "R-duplicate" },
                    new JsonObject { ["workstationId"] = "N-3", ["status"] = "confirmed", ["roseta"] = "R-duplicate" },
                    new JsonObject { ["workstationId"] = "N-missing", ["status"] = "confirmed" })
            });
            Write("positions.json", new JsonObject
            {
                ["positions"] = new JsonArray(
                    new JsonObject { ["mapId"] = "north", ["seatId"] = "N-1", ["x"] = .1, ["y"] = .2 },
                    new JsonObject { ["mapId"] = "north", ["seatId"] = "N-missing", ["x"] = .8, ["y"] = .9 })
            });
            Write("events.json", new JsonObject { ["events"] = new JsonArray() });
            Write("scenarios.json", new JsonObject { ["scenarios"] = new JsonArray() });
            Write("people.json", new JsonObject { ["people"] = new JsonArray() });
            Write("devices.json", new JsonObject { ["devices"] = new JsonArray() });
            Write("locations.json", new JsonObject { ["locations"] = new JsonArray() });
            Write("state.json", new JsonObject { ["schemaVersion"] = "1.0", ["revision"] = 0 });
            if (blockLogs) File.WriteAllText(LogsRoot, "blocked");
            Store = DataStore.FromConfig(new AppConfig { NetworkRoot = _root, DataFolder = "data", BackupFolder = "backups", LogsFolder = "logs", BackupRetentionMode = "disabled" });
        }

        internal DataStore Store { get; }
        private string LogsRoot => Path.Combine(_root, "logs");

        internal JsonObject[] AuditEntries() => !Directory.Exists(LogsRoot)
            ? []
            : Directory.EnumerateFiles(LogsRoot, "audit-*.log")
                .SelectMany(path => File.ReadLines(path))
                .Select(line => JsonNode.Parse(line)?.AsObject() ?? throw new InvalidOperationException("Audit entry is invalid JSON."))
                .ToArray();

        internal JsonObject ReadJsonFile(string path) => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new InvalidOperationException($"{path} is invalid JSON.");

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private void Write(string file, JsonObject value) => File.WriteAllText(Path.Combine(_data, file), value.ToJsonString());
    }

    private static string Text(JsonNode? value) => value?.ToString() ?? "";
}
