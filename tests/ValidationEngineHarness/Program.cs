using System.Text.Json;
using System.Text.Json.Nodes;
using PlanoOpenSpaceIT.Windows;

var tests = new (string Name, Action Test)[]
{
    ("valid input", ValidInputProducesNoResults),
    ("duplicate network outlet", DetectsDuplicateNetworkOutlet),
    ("duplicate device", DetectsDuplicateDevice),
    ("duplicate person", DetectsDuplicatePerson),
    ("missing workspace", DetectsAssignmentMissingWorkspace),
    ("historical occupied marker", DetectsHistoricalOccupiedWithoutAssignment),
    ("operational policy excludes historical diagnostics", OperationalPolicyExcludesHistoricalDiagnostics),
    ("invalid coordinates and deterministic ordering", DetectsInvalidCoordinatesAndOrdersResults),
    ("scenario effective state", ValidatesScenarioEffectiveState),
    ("scenario determinism", ScenarioResultsAreDeterministic)
};

var passed = 0;
foreach (var (name, test) in tests)
{
    try
    {
        test();
        passed++;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"FAIL: {name}: {exception.Message}");
    }
}

Console.WriteLine($"ValidationEngineHarness: {passed}/{tests.Length} passed, {tests.Length - passed} failed");
return passed == tests.Length ? 0 : 1;

static void ValidInputProducesNoResults()
{
    var results = Run(
        Maps(("norte", new[] { Seat("A", 0, 1), Seat("B", 1, 0) })),
        Assignments());

    Equal(0, results.Count, "A valid dataset must produce no validation results.");
}

static void DetectsDuplicateNetworkOutlet()
{
    var results = Run(ValidMaps(), Assignments(
        Assignment("A", roseta: " R-1 "),
        Assignment("B", roseta: "r-1")));

    var result = Single(results, "duplicate-network-outlet");
    Equal(ValidationSeverity.Critical, result.Severity, "Network outlet duplicates must be critical.");
    Equal("R-1", result.EntityId, "The duplicate outlet identifier must be trimmed.");
    SequenceEqual(new[] { "A", "B" }, result.RelatedEntityIds, "Related workstations must be sorted.");
}

static void DetectsDuplicateDevice()
{
    var results = Run(ValidMaps(), Assignments(
        Assignment("A", deviceId: "PC-1"),
        Assignment("B", deviceId: "pc-1")));

    Equal(ValidationSeverity.Critical, Single(results, "duplicate-device").Severity, "Device duplicates must be critical.");
}

static void DetectsDuplicatePerson()
{
    var results = Run(ValidMaps(), Assignments(
        Assignment("A", personId: "Ana"),
        Assignment("B", personId: "ana")));

    Equal(ValidationSeverity.Warning, Single(results, "duplicate-person").Severity, "Person duplicates must be warnings.");
}

static void DetectsAssignmentMissingWorkspace()
{
    var results = Run(ValidMaps(), Assignments(Assignment("missing")));

    var result = Single(results, "assignment-missing-workspace");
    Equal(ValidationSeverity.Critical, result.Severity, "Missing workstation references must be critical.");
    Equal("missing", result.EntityId, "The missing workstation must be identified.");
}

static void DetectsHistoricalOccupiedWithoutAssignment()
{
    var results = Run(
        Maps(("norte", new[] { Seat("occupied", 0.5, 0.5, "occupied") })),
        Assignments());

    var result = Single(results, "historical-occupied-without-assignment");
    Equal(ValidationSeverity.Info, result.Severity, "Historical occupied markers must be informational.");
    Equal(ValidationClassification.Historical, result.Classification, "Historical occupied markers must be classified explicitly.");
    Assert(!result.IsOperational, "Historical occupied markers must not be operational problems.");
    Equal("norte", result.MapId, "The historical marker map must be retained.");
}

static void OperationalPolicyExcludesHistoricalDiagnostics()
{
    var results = Run(
        Maps(("norte", new[] { Seat("occupied", 0.5, 0.5, "occupied"), Seat("invalid", 2, 0.5) })),
        Assignments(Assignment("missing")));
    var operational = ValidationEngine.OperationalResults(results);

    Equal(3, results.Count, "The engine retains the historical diagnostic alongside active validation results.");
    Equal(2, operational.Count, "Operational projection excludes the historical diagnostic.");
    Assert(operational.All(result => result.IsOperational), "Operational projection contains only operational results.");
    Assert(operational.Any(result => result.RuleId == "assignment-missing-workspace" && result.Severity == ValidationSeverity.Critical), "Active missing-workspace validation remains operational.");
    Assert(operational.Any(result => result.RuleId == "invalid-coordinate" && result.Severity == ValidationSeverity.Critical), "Active coordinate validation remains operational.");
}

static void DetectsInvalidCoordinatesAndOrdersResults()
{
    var maps = Maps(("sur", new[]
    {
        Seat("B", 0.5, 2),
        Seat("A", "not-a-number", 0.5),
        Seat("C", 0, 0)
    }));
    var assignments = Assignments(
        Assignment("A", roseta: "R-1", deviceId: "PC-1", personId: "Ana"),
        Assignment("B", roseta: "r-1", deviceId: "pc-1", personId: "ana"),
        Assignment("missing"));

    var first = Run(maps, assignments);
    var second = Run(maps, assignments);

    Equal(6, first.Count, "The fixture must exercise all relevant results.");
    SequenceEqual(first.Select(result => result.Id), second.Select(result => result.Id), "Results must be deterministic.");
    SequenceEqual(
        new[]
        {
            "assignment-missing-workspace|missing|",
            "duplicate-device|PC-1|A,B",
            "duplicate-network-outlet|R-1|A,B",
            "invalid-coordinate|A|",
            "invalid-coordinate|B|",
            "duplicate-person|Ana|A,B"
        },
        first.Select(result => result.Id),
        "Results must preserve severity, rule, map, and entity ordering.");
}

static void ValidatesScenarioEffectiveState()
{
    var reality = Assignments(Assignment("A", roseta: "R-17"), Assignment("B", roseta: "R-22"));
    var scenario = Assignments(Assignment("A", roseta: "R-17"), Assignment("B", roseta: "R-17"));
    var correctedScenario = Assignments(Assignment("A", roseta: "R-17"), Assignment("B", roseta: "R-23"));

    Equal(0, Run(ValidMaps(), reality).Count(result => result.RuleId == "duplicate-network-outlet"), "Reality must remain valid.");
    var conflict = Single(Run(ValidMaps(), scenario), "duplicate-network-outlet");
    SequenceEqual(new[] { "A", "B" }, conflict.RelatedEntityIds, "Scenario conflict must preserve navigation targets.");
    Equal(0, Run(ValidMaps(), correctedScenario).Count(result => result.RuleId == "duplicate-network-outlet"), "Correcting the scenario effective state must remove the conflict.");
}

static void ScenarioResultsAreDeterministic()
{
    var effectiveScenario = Assignments(Assignment("A", deviceId: "PC-17"), Assignment("B", deviceId: "PC-17"));
    var first = Run(ValidMaps(), effectiveScenario);
    var second = Run(ValidMaps(), effectiveScenario);
    SequenceEqual(first.Select(result => $"{result.Id}|{result.Severity}|{string.Join(',', result.RelatedEntityIds)}"), second.Select(result => $"{result.Id}|{result.Severity}|{string.Join(',', result.RelatedEntityIds)}"), "Scenario results must be deterministic.");
}

static IReadOnlyList<ValidationResult> Run(JsonObject maps, JsonObject assignments) => ValidationEngine.Run(maps, assignments);

static JsonObject ValidMaps() => Maps(("norte", new[] { Seat("A", 0, 1), Seat("B", 1, 0) }));

static JsonObject Maps(params (string Id, JsonObject[] Seats)[] maps) => new()
{
    ["maps"] = new JsonArray(maps.Select(map => new JsonObject
    {
        ["id"] = map.Id,
        ["seats"] = new JsonArray(map.Seats)
    }).ToArray())
};

static JsonObject Seat(string id, object? x, object? y, string type = "free") => new()
{
    ["id"] = id,
    ["x"] = JsonNode.Parse(JsonSerializer.Serialize(x)),
    ["y"] = JsonNode.Parse(JsonSerializer.Serialize(y)),
    ["type"] = type
};

static JsonObject Assignments(params JsonObject[] assignments) => new()
{
    ["assignments"] = new JsonArray(assignments)
};

static JsonObject Assignment(string workstationId, string? roseta = null, string? deviceId = null, string? personId = null)
{
    var assignment = new JsonObject { ["workstationId"] = workstationId };
    if (roseta is not null) assignment["roseta"] = roseta;
    if (deviceId is not null) assignment["deviceId"] = deviceId;
    if (personId is not null) assignment["personId"] = personId;
    return assignment;
}

static ValidationResult Single(IEnumerable<ValidationResult> results, string ruleId)
{
    var matches = results.Where(result => result.RuleId == ruleId).ToArray();
    Equal(1, matches.Length, $"Expected exactly one result for {ruleId}.");
    return matches[0];
}

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

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
