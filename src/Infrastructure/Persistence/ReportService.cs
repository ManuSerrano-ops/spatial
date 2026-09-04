using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlanoOpenSpaceIT.Windows;

internal sealed class ReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly Storage _storage;

    internal ReportService(Storage storage)
    {
        _storage = storage;
    }

    internal JsonObject RunValidation(string? scenarioId)
    {
        _storage.LogInfo(new("validation.started", scenarioId));
        var effective = _storage.Load(scenarioId);
        var stopwatch = Stopwatch.StartNew();
        var maps = effective["maps"]?.AsObject() ?? throw new InvalidDataException("Faltan planos.");
        var assignments = effective["assignments"]?.AsObject() ?? throw new InvalidDataException("Faltan asignaciones.");
        var results = ValidationEngine.OperationalResults(ValidationEngine.Run(maps, assignments));
        var counts = results.GroupBy(result => result.Severity).ToDictionary(group => group.Key.ToString().ToLowerInvariant(), group => group.Count());
        var summary = new JsonObject { ["total"] = results.Count, ["critical"] = counts.GetValueOrDefault("critical"), ["warning"] = counts.GetValueOrDefault("warning"), ["info"] = counts.GetValueOrDefault("info") };
        var durationMs = stopwatch.ElapsedMilliseconds;
        _storage.LogInfo(new("validation.finished", scenarioId, results.Count, durationMs, new Dictionary<string, object?> { ["critical"] = counts.GetValueOrDefault("critical"), ["warning"] = counts.GetValueOrDefault("warning"), ["info"] = counts.GetValueOrDefault("info") }));
        return new JsonObject { ["results"] = new JsonArray(results.Select(ValidationJson).ToArray()), ["summary"] = summary, ["count"] = results.Count, ["durationMs"] = durationMs };
    }

    internal JsonObject RunSpatialAnalytics(string? scenarioId)
    {
        var normalizedScenarioId = NormalizeScenarioId(scenarioId);
        var effective = _storage.Load(normalizedScenarioId);
        var maps = effective["maps"]?.AsObject() ?? throw new InvalidDataException("Faltan planos.");
        var assignments = effective["assignments"]?.AsObject() ?? throw new InvalidDataException("Faltan asignaciones.");
        var stopwatch = Stopwatch.StartNew();
        var validation = ValidationEngine.OperationalResults(ValidationEngine.Run(maps, assignments));
        IReadOnlyList<ScenarioDiffChange>? changes = null;
        SpatialAnalyticsReport? baseline = null;
        if (normalizedScenarioId is not null)
        {
            var scenario = _storage.FindScenario(normalizedScenarioId);
            var baseState = scenario["base"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
            var draftState = scenario["draft"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
            var baseMaps = baseState["maps"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
            var baseAssignments = baseState["assignments"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
            var baseValidation = ValidationEngine.OperationalResults(ValidationEngine.Run(baseMaps, baseAssignments));
            changes = ScenarioDiffEngine.Compare(baseState, draftState, baseValidation, validation).Changes;
            baseline = SpatialAnalyticsEngine.Analyze(baseMaps, baseAssignments, baseValidation);
        }
        var report = SpatialAnalyticsEngine.Analyze(maps, assignments, validation, changes);
        var durationMs = stopwatch.ElapsedMilliseconds;
        _storage.LogInfo(new("analytics.finished", normalizedScenarioId, report.Totals.Total, durationMs, new Dictionary<string, object?> { ["occupied"] = report.Totals.Occupied, ["free"] = report.Totals.Free, ["reserved"] = report.Totals.Reserved, ["problems"] = report.Validation.Total, ["scenarioChanges"] = report.Scenario?.TotalChanges ?? 0 }));
        return new JsonObject { ["contextScenarioId"] = normalizedScenarioId, ["result"] = SpatialAnalyticsJson(report), ["baseline"] = baseline is null ? null : SpatialAnalyticsJson(baseline), ["durationMs"] = durationMs };
    }

    internal JsonObject GetIntegrityReport()
    {
        var document = IntegrityReport.Build(_storage.ReadRequired("maps.json"), _storage.ReadRequired("assignments.json"), _storage.ReadRequired("positions.json"));
        WriteIntegrityReport(document);
        return document;
    }

    private void WriteIntegrityReport(JsonObject document)
    {
        try
        {
            _storage.CreateDirectory(_storage.LogsRoot);
            var path = Path.Combine(_storage.LogsRoot, $"integrity-report-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffffffZ}.json");
            document["reportPath"] = path;
            _storage.WriteText(path, document.ToJsonString(JsonOptions));
            var count = document["counts"]?.AsObject()?.Select(item => item.Value?.GetValue<int>() ?? 0).Sum() ?? 0;
            _storage.LogInfo(new("integrity.report", Count: count, CurrentRevision: _storage.CurrentRevision(), ReportPath: path));
        }
        catch (Exception exception)
        {
            document.Remove("reportPath");
            _storage.LogError("integrity.report.failed", exception);
        }
    }

    internal static JsonObject ValidationJson(ValidationResult result) => new()
    {
        ["id"] = result.Id, ["ruleId"] = result.RuleId, ["severity"] = result.Severity.ToString(), ["classification"] = result.Classification.ToString(), ["operational"] = result.IsOperational,
        ["entityType"] = result.EntityType, ["entityId"] = result.EntityId, ["mapId"] = result.MapId,
        ["field"] = result.Field, ["title"] = result.Title, ["message"] = result.Message,
        ["details"] = result.Details, ["relatedEntities"] = new JsonArray(result.RelatedEntityIds.Select(id => (JsonNode?)id).ToArray()),
        ["suggestedAction"] = result.SuggestedAction
    };

    private static JsonObject SpatialAnalyticsJson(SpatialAnalyticsReport report) => new()
    {
        ["totals"] = SeatMetricsJson(report.Totals),
        ["validation"] = ValidationTotalsJson(report.Validation),
        ["maps"] = new JsonArray(report.Maps.Select(map => new JsonObject { ["mapId"] = map.MapId, ["mapName"] = map.MapName, ["seats"] = SeatMetricsJson(map.Seats), ["validation"] = ValidationTotalsJson(map.Validation) }).ToArray()),
        ["heatmapPoints"] = new JsonArray(report.HeatmapPoints.Select(point => new JsonObject { ["mapId"] = point.MapId, ["mapName"] = point.MapName, ["seatId"] = point.SeatId, ["x"] = point.X, ["y"] = point.Y, ["layer"] = point.Layer, ["value"] = point.Value, ["sourceId"] = point.SourceId }).ToArray()),
        ["scenario"] = report.Scenario is null ? null : new JsonObject { ["totalChanges"] = report.Scenario.TotalChanges, ["mappedChanges"] = report.Scenario.MappedChanges }
    };

    private static JsonObject SeatMetricsJson(SpatialSeatMetrics metrics) => new() { ["total"] = metrics.Total, ["occupied"] = metrics.Occupied, ["free"] = metrics.Free, ["reserved"] = metrics.Reserved, ["occupancyRate"] = metrics.OccupancyRate, ["availabilityRate"] = metrics.AvailabilityRate };
    private static JsonObject ValidationTotalsJson(SpatialValidationTotals metrics) => new() { ["total"] = metrics.Total, ["critical"] = metrics.Critical, ["warning"] = metrics.Warning, ["info"] = metrics.Info };
    private static string? NormalizeScenarioId(string? scenarioId) => string.IsNullOrWhiteSpace(scenarioId) ? null : scenarioId;

    internal sealed record Storage(
        Func<string?, JsonObject> Load,
        Func<string, JsonObject> FindScenario,
        Func<string, JsonObject> ReadRequired,
        string LogsRoot,
        Action<string> CreateDirectory,
        Action<string, string> WriteText,
        Func<long> CurrentRevision,
        Action<ReportAudit> LogInfo,
        Action<string, Exception> LogError);

    internal sealed record ReportAudit(
        string Action,
        string? ScenarioId = null,
        int? Count = null,
        long? DurationMs = null,
        IReadOnlyDictionary<string, object?>? Details = null,
        long? CurrentRevision = null,
        string? ReportPath = null);
}
