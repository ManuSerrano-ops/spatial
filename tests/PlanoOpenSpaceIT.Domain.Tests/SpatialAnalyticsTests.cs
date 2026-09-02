using System.Text.Json;
using System.Text.Json.Nodes;
using PlanoOpenSpaceIT.Windows;
using Xunit;

namespace PlanoOpenSpaceIT.Domain.Tests;

public sealed class SpatialAnalyticsTests
{
    [Fact]
    public void EmptyInput()
    {
        var report = Analyze(Maps(), Assignments());

        Assert.Equal(0, report.Totals.Total);
        Assert.Equal(0m, report.Totals.OccupancyRate);
        Assert.Equal(0m, report.Totals.AvailabilityRate);
        Assert.Empty(report.Maps);
        Assert.Empty(report.HeatmapPoints);
        Assert.True(report.Scenario is null, "Scenario metrics are omitted when no changes are supplied.");
    }

    [Fact]
    public void TotalsStatesAndRates()
    {
        var report = Analyze(
            Maps(("norte", "Norte", new[] { Seat("A", .1, .1), Seat("B", .2, .2), Seat("C", .3, .3), Seat("D", .4, .4) })),
            Assignments(Assignment("B", personId: "P-1"), Assignment("C", status: "reserved"), Assignment("D")));

        Assert.Equal(4, report.Totals.Total);
        Assert.Equal(1, report.Totals.Occupied);
        Assert.Equal(2, report.Totals.Free);
        Assert.Equal(1, report.Totals.Reserved);
        Assert.Equal(25m, report.Totals.OccupancyRate);
        Assert.Equal(50m, report.Totals.AvailabilityRate);
    }

    [Fact]
    public void SeatLevelCurrentPersonFallback()
    {
        var legacyCurrentPerson = Seat("legacy", .2, .2);
        legacyCurrentPerson["personId"] = "person-legacy";
        var report = Analyze(Maps(("norte", "Norte", new[] { legacyCurrentPerson })), Assignments());

        Assert.Equal(1, report.Totals.Occupied);
        Assert.Equal(0, report.Totals.Free);
        Assert.True(report.HeatmapPoints.Any(point => point.Layer == "occupancy" && point.SeatId == "legacy"), "Heatmap occupancy uses the same effective state.");
    }

    [Fact]
    public void PerMapMetrics()
    {
        var report = Analyze(
            Maps(
                ("sur", "Sur", new[] { Seat("S1", .1, .1), Seat("S2", .2, .2) }),
                ("norte", "Norte", new[] { Seat("N1", .3, .3) })),
            Assignments(Assignment("S1", personId: "P-1"), Assignment("N1", status: "reserved")));

        DomainTestSupport.SequenceEqual(["norte", "sur"], report.Maps.Select(map => map.MapId), "Map metrics use ordinal map ordering.");
        var sur = report.Maps.Single(map => map.MapId == "sur");
        Assert.Equal(2, sur.Seats.Total);
        Assert.Equal(1, sur.Seats.Occupied);
        Assert.Equal(1, sur.Seats.Free);
        var norte = report.Maps.Single(map => map.MapId == "norte");
        Assert.Equal(1, norte.Seats.Reserved);
    }

    [Fact]
    public void ConfiguredEmptyMapRemainsVisible()
    {
        var report = Analyze(Maps(("norte", "Norte", new[] { Seat("N1", .2, .2) }), ("qc", "Quality Control", [])), Assignments());

        Assert.Equal(2, report.Maps.Count);
        var qc = report.Maps.Single(map => map.MapId == "qc");
        Assert.Equal("Quality Control", qc.MapName);
        Assert.Equal(0, qc.Seats.Total);
        Assert.Equal(0m, qc.Seats.OccupancyRate);
        Assert.Equal(0m, qc.Seats.AvailabilityRate);
        Assert.Equal(0, qc.Validation.Total);
    }

    [Fact]
    public void ValidationAggregation()
    {
        var validation = new[]
        {
            Result("critical", ValidationSeverity.Critical, "norte", "A"),
            Result("warning", ValidationSeverity.Warning, "norte", "A"),
            Result("info", ValidationSeverity.Info, "sur", "B"),
            Result("global", ValidationSeverity.Critical, null, "missing", "assignment")
        };
        var report = Analyze(
            Maps(("sur", "Sur", new[] { Seat("B", .1, .1) }), ("norte", "Norte", new[] { Seat("A", .2, .2) })),
            Assignments(),
            validation);

        Assert.Equal(2, report.Validation.Critical);
        Assert.Equal(1, report.Validation.Warning);
        Assert.Equal(1, report.Validation.Info);
        Assert.Equal(2, report.Maps.Single(map => map.MapId == "norte").Validation.Total);
        Assert.Equal(1, report.Maps.Single(map => map.MapId == "sur").Validation.Info);
    }

    [Fact]
    public void HistoricalDiagnosticsAreExcluded()
    {
        var report = Analyze(
            Maps(("norte", "Norte", new[] { Seat("A", .2, .2) })),
            Assignments(),
            new[]
            {
                Result("historical-A", ValidationSeverity.Info, "norte", "A", classification: ValidationClassification.Historical),
                Result("active-A", ValidationSeverity.Warning, "norte", "A")
            });

        Assert.Equal(0, report.Validation.Info);
        Assert.Equal(1, report.Validation.Warning);
        Assert.Equal(1, report.Maps.Single().Validation.Total);
        Assert.True(!report.HeatmapPoints.Any(point => point.SourceId == "historical-A"), "Historical diagnostics do not create Problems heatmap points.");
        Assert.True(report.HeatmapPoints.Any(point => point.SourceId == "active-A"), "Operational problems remain in Problems heatmap points.");
    }

    [Fact]
    public void ScenarioLikeDifferentStates()
    {
        var maps = Maps(("norte", "Norte", new[] { Seat("A", .5, .5) }));
        var reality = Analyze(maps, Assignments());
        var scenario = Analyze(maps, Assignments(Assignment("A", personId: "P-1")));

        Assert.Equal(1, reality.Totals.Free);
        Assert.Equal(1, scenario.Totals.Occupied);
        Assert.Equal(0m, reality.Totals.OccupancyRate);
        Assert.Equal(100m, scenario.Totals.OccupancyRate);
    }

    [Fact]
    public void DeterministicOutput()
    {
        var maps = Maps(
            ("sur", "Sur", new[] { Seat("B", .8, .2), Seat("A", .2, .8) }),
            ("norte", "Norte", new[] { Seat("C", .5, .5) }));
        var assignments = Assignments(Assignment("B", personId: "P-2"));
        var validation = new[] { Result("z", ValidationSeverity.Warning, "sur", "B"), Result("a", ValidationSeverity.Critical, "sur", "A") };

        var first = Analyze(maps, assignments, validation);
        var second = Analyze(maps, assignments, validation);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void HeatmapPoints()
    {
        var report = Analyze(
            Maps(("norte", "Norte", new[]
            {
                Seat("A", .1, .1), Seat("B", .2, .2), Seat("C", .3, .3)
            })),
            Assignments(Assignment("B", personId: "P-1"), Assignment("C", status: "reserved")),
            new[]
            {
                Result("critical-B", ValidationSeverity.Critical, "norte", "B"),
                Result("warning-A", ValidationSeverity.Warning, "norte", "A")
            });

        var points = report.HeatmapPoints;
        Assert.Equal(4, points.Count);
        Assert.Equal(1, points.Single(point => point.Layer == "availability" && point.SeatId == "A").Value);
        Assert.Equal(1, points.Single(point => point.Layer == "occupancy" && point.SeatId == "B").Value);
        Assert.Equal(3, points.Single(point => point.SourceId == "critical-B").Value);
        Assert.Equal(2, points.Single(point => point.SourceId == "warning-A").Value);
    }

    [Fact]
    public void InvalidCoordinatesIgnored()
    {
        var report = Analyze(
            Maps(("norte", "Norte", new[] { Seat("valid", 0, 1), Seat("bad-x", -0.1, .5), Seat("bad-y", .5, 2) })),
            Assignments(),
            new[] { Result("invalid", ValidationSeverity.Critical, "norte", "bad-x") });

        Assert.Equal(3, report.Totals.Total);
        var point = Assert.Single(report.HeatmapPoints);
        Assert.Equal("valid", point.SeatId);
    }

    [Fact]
    public void ScenarioChangeDensity()
    {
        var maps = Maps(("norte", "Norte", new[] { Seat("A", .1, .1) }));
        var changes = new[]
        {
            Change("workspace-direct", "workspace", "B", "norte", new JsonObject { ["x"] = .7, ["y"] = .3 }),
            Change("assignment-fallback", "assignment", "A", "norte", null),
            Change("invalid-coordinate", "workspace", "missing", "norte", new JsonObject { ["x"] = 2, ["y"] = .3 })
        };
        var report = Analyze(maps, Assignments(), scenarioChanges: changes);

        Assert.Equal(3, report.Scenario!.TotalChanges);
        Assert.Equal(2, report.Scenario.MappedChanges);
        var points = report.HeatmapPoints.Where(point => point.Layer == "scenario-changes").ToArray();
        Assert.Equal(2, points.Length);
        Assert.True(points.All(point => point.Value == 1), "Every scenario change has a density value of one.");
    }

    private static SpatialAnalyticsReport Analyze(JsonObject maps, JsonObject assignments, IReadOnlyList<ValidationResult>? validation = null, IReadOnlyList<ScenarioDiffChange>? scenarioChanges = null) =>
        SpatialAnalyticsEngine.Analyze(maps, assignments, validation, scenarioChanges);

    private static JsonObject Maps(params (string Id, string Name, JsonObject[] Seats)[] maps) => new()
    {
        ["maps"] = new JsonArray(maps.Select(map => new JsonObject
        {
            ["id"] = map.Id,
            ["name"] = map.Name,
            ["seats"] = new JsonArray(map.Seats)
        }).ToArray())
    };

    private static JsonObject Assignments(params JsonObject[] assignments) => new()
    {
        ["assignments"] = new JsonArray(assignments)
    };

    private static JsonObject Seat(string id, object? x, object? y) => new()
    {
        ["id"] = id,
        ["x"] = Node(x),
        ["y"] = Node(y)
    };

    private static JsonObject Assignment(string workstationId, string? personId = null, string? status = null)
    {
        var assignment = new JsonObject { ["workstationId"] = workstationId };
        if (personId is not null) assignment["personId"] = personId;
        if (status is not null) assignment["status"] = status;
        return assignment;
    }

    private static ValidationResult Result(string id, ValidationSeverity severity, string? mapId, string entityId, string entityType = "workspace", ValidationClassification classification = ValidationClassification.Operational) =>
        new(id, "test", severity, entityType, entityId, mapId, null, "Test", "Test", null, [], "Test", classification);

    private static ScenarioDiffChange Change(string id, string entityType, string entityId, string mapId, JsonObject? after) =>
        new(id, ScenarioChangeKind.Modified, entityType, entityId, mapId, "Norte", null, null, [], null, after);

    private static JsonNode? Node(object? value) => JsonNode.Parse(JsonSerializer.Serialize(value));
}
