using System.Text.Json;
using System.Text.Json.Nodes;
using PlanoOpenSpaceIT.Windows;

var tests = new (string Name, Action Test)[]
{
    ("empty input", EmptyInput),
    ("totals states and rates", TotalsStatesAndRates),
    ("seat-level current person fallback", SeatLevelCurrentPersonFallback),
    ("per-map metrics", PerMapMetrics),
    ("configured empty map remains visible", ConfiguredEmptyMapRemainsVisible),
    ("validation aggregation", ValidationAggregation),
    ("historical diagnostics excluded from totals and heatmap", HistoricalDiagnosticsAreExcluded),
    ("scenario-like different states", ScenarioLikeDifferentStates),
    ("deterministic output", DeterministicOutput),
    ("heatmap points", HeatmapPoints),
    ("invalid coordinates ignored", InvalidCoordinatesIgnored),
    ("scenario change density", ScenarioChangeDensity)
};

var passed = 0;
foreach (var (name, test) in tests)
{
    try { test(); passed++; }
    catch (Exception exception) { Console.Error.WriteLine($"FAIL: {name}: {exception.Message}"); }
}

Console.WriteLine($"SpatialAnalyticsHarness: {passed}/{tests.Length} passed, {tests.Length - passed} failed");
return passed == tests.Length ? 0 : 1;

static void EmptyInput()
{
    var report = Analyze(Maps(), Assignments());
    Equal(0, report.Totals.Total, "Empty input has no seats.");
    Equal(0m, report.Totals.OccupancyRate, "Empty input has a zero occupancy rate.");
    Equal(0m, report.Totals.AvailabilityRate, "Empty input has a zero availability rate.");
    Equal(0, report.Maps.Count, "Empty input has no map metrics.");
    Equal(0, report.HeatmapPoints.Count, "Empty input has no points.");
    Assert(report.Scenario is null, "Scenario metrics are omitted when no changes are supplied.");
}

static void TotalsStatesAndRates()
{
    var report = Analyze(
        Maps(("norte", "Norte", new[] { Seat("A", .1, .1), Seat("B", .2, .2), Seat("C", .3, .3), Seat("D", .4, .4) })),
        Assignments(Assignment("B", personId: "P-1"), Assignment("C", status: "reserved"), Assignment("D")));

    Equal(4, report.Totals.Total, "All seats are counted.");
    Equal(1, report.Totals.Occupied, "Person assignments produce occupied seats.");
    Equal(2, report.Totals.Free, "Missing or personless assignments produce free seats.");
    Equal(1, report.Totals.Reserved, "Reserved assignments produce reserved seats.");
    Equal(25m, report.Totals.OccupancyRate, "Occupancy is a percent of all seats.");
    Equal(50m, report.Totals.AvailabilityRate, "Availability is the free-seat percent.");
}

static void SeatLevelCurrentPersonFallback()
{
    var legacyCurrentPerson = Seat("legacy", .2, .2);
    legacyCurrentPerson["personId"] = "person-legacy";
    var report = Analyze(Maps(("norte", "Norte", new[] { legacyCurrentPerson })), Assignments());

    Equal(1, report.Totals.Occupied, "A seat-level current person fallback is operationally occupied.");
    Equal(0, report.Totals.Free, "A seat-level current person fallback is not counted as free.");
    Assert(report.HeatmapPoints.Any(point => point.Layer == "occupancy" && point.SeatId == "legacy"), "Heatmap occupancy uses the same effective state.");
}

static void PerMapMetrics()
{
    var report = Analyze(
        Maps(
            ("sur", "Sur", new[] { Seat("S1", .1, .1), Seat("S2", .2, .2) }),
            ("norte", "Norte", new[] { Seat("N1", .3, .3) })),
        Assignments(Assignment("S1", personId: "P-1"), Assignment("N1", status: "reserved")));

    SequenceEqual(new[] { "norte", "sur" }, report.Maps.Select(map => map.MapId), "Map metrics use ordinal map ordering.");
    var sur = report.Maps.Single(map => map.MapId == "sur");
    Equal(2, sur.Seats.Total, "Map totals are derived from that map's seats.");
    Equal(1, sur.Seats.Occupied, "Map state is derived from assignments.");
    Equal(1, sur.Seats.Free, "Map free seats are retained.");
    var norte = report.Maps.Single(map => map.MapId == "norte");
    Equal(1, norte.Seats.Reserved, "Reserved seats are reported per map.");
}

static void ConfiguredEmptyMapRemainsVisible()
{
    var report = Analyze(Maps(("norte", "Norte", new[] { Seat("N1", .2, .2) }), ("qc", "Quality Control", [])), Assignments());
    Equal(2, report.Maps.Count, "Every configured map is present.");
    var qc = report.Maps.Single(map => map.MapId == "qc");
    Equal("Quality Control", qc.MapName, "Empty map preserves configured name.");
    Equal(0, qc.Seats.Total, "Empty map total is zero.");
    Equal(0m, qc.Seats.OccupancyRate, "Empty map has no occupancy rate denominator.");
    Equal(0m, qc.Seats.AvailabilityRate, "Empty map has no availability rate denominator.");
    Equal(0, qc.Validation.Total, "Empty map has no validation rows by default.");
}

static void ValidationAggregation()
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

    Equal(2, report.Validation.Critical, "Global severity totals include every result.");
    Equal(1, report.Validation.Warning, "Warnings are aggregated.");
    Equal(1, report.Validation.Info, "Information results are aggregated.");
    Equal(2, report.Maps.Single(map => map.MapId == "norte").Validation.Total, "Map validation is keyed by map ID.");
    Equal(1, report.Maps.Single(map => map.MapId == "sur").Validation.Info, "Per-map severity totals are retained.");
}

static void HistoricalDiagnosticsAreExcluded()
{
    var report = Analyze(
        Maps(("norte", "Norte", new[] { Seat("A", .2, .2) })),
        Assignments(),
        new[]
        {
            Result("historical-A", ValidationSeverity.Info, "norte", "A", classification: ValidationClassification.Historical),
            Result("active-A", ValidationSeverity.Warning, "norte", "A")
        });

    Equal(0, report.Validation.Info, "Historical diagnostics do not contribute to global information totals.");
    Equal(1, report.Validation.Warning, "Operational warnings remain in global totals.");
    Equal(1, report.Maps.Single().Validation.Total, "Per-map totals exclude historical diagnostics.");
    Assert(!report.HeatmapPoints.Any(point => point.SourceId == "historical-A"), "Historical diagnostics do not create Problems heatmap points.");
    Assert(report.HeatmapPoints.Any(point => point.SourceId == "active-A"), "Operational problems remain in Problems heatmap points.");
}

static void ScenarioLikeDifferentStates()
{
    var maps = Maps(("norte", "Norte", new[] { Seat("A", .5, .5) }));
    var reality = Analyze(maps, Assignments());
    var scenario = Analyze(maps, Assignments(Assignment("A", personId: "P-1")));

    Equal(1, reality.Totals.Free, "The effective reality is free.");
    Equal(1, scenario.Totals.Occupied, "The scenario effective state is occupied.");
    Equal(0m, reality.Totals.OccupancyRate, "Reality occupancy is independently calculated.");
    Equal(100m, scenario.Totals.OccupancyRate, "Scenario occupancy is independently calculated.");
}

static void DeterministicOutput()
{
    var maps = Maps(
        ("sur", "Sur", new[] { Seat("B", .8, .2), Seat("A", .2, .8) }),
        ("norte", "Norte", new[] { Seat("C", .5, .5) }));
    var assignments = Assignments(Assignment("B", personId: "P-2"));
    var validation = new[] { Result("z", ValidationSeverity.Warning, "sur", "B"), Result("a", ValidationSeverity.Critical, "sur", "A") };

    var first = Analyze(maps, assignments, validation);
    var second = Analyze(maps, assignments, validation);
    Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second), "Equivalent input must produce serializably identical output.");
}

static void HeatmapPoints()
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
    Equal(4, points.Count, "Free, occupied, and validation evidence produce points; reserved alone does not.");
    Equal(1, points.Single(point => point.Layer == "availability" && point.SeatId == "A").Value, "Free-seat availability value is one.");
    Equal(1, points.Single(point => point.Layer == "occupancy" && point.SeatId == "B").Value, "Occupied-seat occupancy value is one.");
    Equal(3, points.Single(point => point.SourceId == "critical-B").Value, "Critical problems have value three.");
    Equal(2, points.Single(point => point.SourceId == "warning-A").Value, "Warnings have value two.");
}

static void InvalidCoordinatesIgnored()
{
    var report = Analyze(
        Maps(("norte", "Norte", new[] { Seat("valid", 0, 1), Seat("bad-x", -0.1, .5), Seat("bad-y", .5, 2) })),
        Assignments(),
        new[] { Result("invalid", ValidationSeverity.Critical, "norte", "bad-x") });

    Equal(3, report.Totals.Total, "Invalid coordinates do not remove seats from state metrics.");
    Equal(1, report.HeatmapPoints.Count, "Only valid normalized coordinates create heatmap points.");
    Equal("valid", report.HeatmapPoints[0].SeatId, "The valid seat remains represented.");
}

static void ScenarioChangeDensity()
{
    var maps = Maps(("norte", "Norte", new[] { Seat("A", .1, .1) }));
    var changes = new[]
    {
        Change("workspace-direct", "workspace", "B", "norte", new JsonObject { ["x"] = .7, ["y"] = .3 }),
        Change("assignment-fallback", "assignment", "A", "norte", null),
        Change("invalid-coordinate", "workspace", "missing", "norte", new JsonObject { ["x"] = 2, ["y"] = .3 })
    };
    var report = Analyze(maps, Assignments(), scenarioChanges: changes);

    Equal(3, report.Scenario!.TotalChanges, "All supplied changes are counted.");
    Equal(2, report.Scenario.MappedChanges, "Only changes with valid usable coordinates are mapped.");
    var points = report.HeatmapPoints.Where(point => point.Layer == "scenario-changes").ToArray();
    Equal(2, points.Length, "Scenario density contributes one point per mapped change.");
    Assert(points.All(point => point.Value == 1), "Every scenario change has a density value of one.");
}

static SpatialAnalyticsReport Analyze(JsonObject maps, JsonObject assignments, IReadOnlyList<ValidationResult>? validation = null, IReadOnlyList<ScenarioDiffChange>? scenarioChanges = null) =>
    SpatialAnalyticsEngine.Analyze(maps, assignments, validation, scenarioChanges);


static JsonObject Maps(params (string Id, string Name, JsonObject[] Seats)[] maps) => new()
{
    ["maps"] = new JsonArray(maps.Select(map => new JsonObject
    {
        ["id"] = map.Id,
        ["name"] = map.Name,
        ["seats"] = new JsonArray(map.Seats)
    }).ToArray())
};

static JsonObject Assignments(params JsonObject[] assignments) => new()
{
    ["assignments"] = new JsonArray(assignments)
};

static JsonObject Seat(string id, object? x, object? y) => new()
{
    ["id"] = id,
    ["x"] = Node(x),
    ["y"] = Node(y)
};

static JsonObject Assignment(string workstationId, string? personId = null, string? status = null)
{
    var assignment = new JsonObject { ["workstationId"] = workstationId };
    if (personId is not null) assignment["personId"] = personId;
    if (status is not null) assignment["status"] = status;
    return assignment;
}

static ValidationResult Result(string id, ValidationSeverity severity, string? mapId, string entityId, string entityType = "workspace", ValidationClassification classification = ValidationClassification.Operational) =>
    new(id, "test", severity, entityType, entityId, mapId, null, "Test", "Test", null, [], "Test", classification);

static ScenarioDiffChange Change(string id, string entityType, string entityId, string mapId, JsonObject? after) =>
    new(id, ScenarioChangeKind.Modified, entityType, entityId, mapId, "Norte", null, null, [], null, after);

static JsonNode? Node(object? value) => JsonNode.Parse(JsonSerializer.Serialize(value));

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message} Expected: {expected}; actual: {actual}.");
}

static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
{
    if (!expected.SequenceEqual(actual))
        throw new InvalidOperationException($"{message} Expected: [{string.Join(", ", expected)}]; actual: [{string.Join(", ", actual)}].");
}

static void Assert(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}
