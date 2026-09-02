using System.Text.Json;
using System.Text.Json.Nodes;
using PlanoOpenSpaceIT.Windows;
using Xunit;

namespace PlanoOpenSpaceIT.Domain.Tests;

public sealed class ValidationEngineTests
{
    [Fact]
    public void ValidInputProducesNoResults()
    {
        var results = Run(
            Maps(("norte", new[] { Seat("A", 0, 1), Seat("B", 1, 0) })),
            Assignments());

        Assert.Empty(results);
    }

    [Fact]
    public void DetectsDuplicateNetworkOutlet()
    {
        var results = Run(ValidMaps(), Assignments(
            Assignment("A", roseta: " R-1 "),
            Assignment("B", roseta: "r-1")));

        var result = Single(results, "duplicate-network-outlet");
        Assert.Equal(ValidationSeverity.Critical, result.Severity);
        Assert.Equal("R-1", result.EntityId);
        DomainTestSupport.SequenceEqual(["A", "B"], result.RelatedEntityIds, "Related workstations must be sorted.");
    }

    [Fact]
    public void DetectsDuplicateDevice()
    {
        var results = Run(ValidMaps(), Assignments(
            Assignment("A", deviceId: "PC-1"),
            Assignment("B", deviceId: "pc-1")));

        Assert.Equal(ValidationSeverity.Critical, Single(results, "duplicate-device").Severity);
    }

    [Fact]
    public void DetectsDuplicatePerson()
    {
        var results = Run(ValidMaps(), Assignments(
            Assignment("A", personId: "Ana"),
            Assignment("B", personId: "ana")));

        Assert.Equal(ValidationSeverity.Warning, Single(results, "duplicate-person").Severity);
    }

    [Fact]
    public void DetectsAssignmentMissingWorkspace()
    {
        var results = Run(ValidMaps(), Assignments(Assignment("missing")));

        var result = Single(results, "assignment-missing-workspace");
        Assert.Equal(ValidationSeverity.Critical, result.Severity);
        Assert.Equal("missing", result.EntityId);
    }

    [Fact]
    public void DetectsHistoricalOccupiedWithoutAssignment()
    {
        var results = Run(
            Maps(("norte", new[] { Seat("occupied", 0.5, 0.5, "occupied") })),
            Assignments());

        var result = Single(results, "historical-occupied-without-assignment");
        Assert.Equal(ValidationSeverity.Info, result.Severity);
        Assert.Equal(ValidationClassification.Historical, result.Classification);
        Assert.True(!result.IsOperational, "Historical occupied markers must not be operational problems.");
        Assert.Equal("norte", result.MapId);
    }

    [Fact]
    public void OperationalPolicyExcludesHistoricalDiagnostics()
    {
        var results = Run(
            Maps(("norte", new[] { Seat("occupied", 0.5, 0.5, "occupied"), Seat("invalid", 2, 0.5) })),
            Assignments(Assignment("missing")));
        var operational = ValidationEngine.OperationalResults(results);

        Assert.Equal(3, results.Count);
        Assert.Equal(2, operational.Count);
        Assert.True(operational.All(result => result.IsOperational), "Operational projection contains only operational results.");
        Assert.True(operational.Any(result => result.RuleId == "assignment-missing-workspace" && result.Severity == ValidationSeverity.Critical), "Active missing-workspace validation remains operational.");
        Assert.True(operational.Any(result => result.RuleId == "invalid-coordinate" && result.Severity == ValidationSeverity.Critical), "Active coordinate validation remains operational.");
    }

    [Fact]
    public void DetectsInvalidCoordinatesAndOrdersResults()
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

        Assert.Equal(6, first.Count);
        DomainTestSupport.SequenceEqual(first.Select(result => result.Id), second.Select(result => result.Id), "Results must be deterministic.");
        DomainTestSupport.SequenceEqual(
            [
                "assignment-missing-workspace|missing|",
                "duplicate-device|PC-1|A,B",
                "duplicate-network-outlet|R-1|A,B",
                "invalid-coordinate|A|",
                "invalid-coordinate|B|",
                "duplicate-person|Ana|A,B"
            ],
            first.Select(result => result.Id),
            "Results must preserve severity, rule, map, and entity ordering.");
    }

    [Fact]
    public void ValidatesScenarioEffectiveState()
    {
        var reality = Assignments(Assignment("A", roseta: "R-17"), Assignment("B", roseta: "R-22"));
        var scenario = Assignments(Assignment("A", roseta: "R-17"), Assignment("B", roseta: "R-17"));
        var correctedScenario = Assignments(Assignment("A", roseta: "R-17"), Assignment("B", roseta: "R-23"));

        Assert.Equal(0, Run(ValidMaps(), reality).Count(result => result.RuleId == "duplicate-network-outlet"));
        var conflict = Single(Run(ValidMaps(), scenario), "duplicate-network-outlet");
        DomainTestSupport.SequenceEqual(["A", "B"], conflict.RelatedEntityIds, "Scenario conflict must preserve navigation targets.");
        Assert.Equal(0, Run(ValidMaps(), correctedScenario).Count(result => result.RuleId == "duplicate-network-outlet"));
    }

    [Fact]
    public void ScenarioResultsAreDeterministic()
    {
        var effectiveScenario = Assignments(Assignment("A", deviceId: "PC-17"), Assignment("B", deviceId: "PC-17"));
        var first = Run(ValidMaps(), effectiveScenario);
        var second = Run(ValidMaps(), effectiveScenario);

        DomainTestSupport.SequenceEqual(
            first.Select(result => $"{result.Id}|{result.Severity}|{string.Join(',', result.RelatedEntityIds)}"),
            second.Select(result => $"{result.Id}|{result.Severity}|{string.Join(',', result.RelatedEntityIds)}"),
            "Scenario results must be deterministic.");
    }

    private static IReadOnlyList<ValidationResult> Run(JsonObject maps, JsonObject assignments) => ValidationEngine.Run(maps, assignments);

    private static JsonObject ValidMaps() => Maps(("norte", new[] { Seat("A", 0, 1), Seat("B", 1, 0) }));

    private static JsonObject Maps(params (string Id, JsonObject[] Seats)[] maps) => new()
    {
        ["maps"] = new JsonArray(maps.Select(map => new JsonObject
        {
            ["id"] = map.Id,
            ["seats"] = new JsonArray(map.Seats)
        }).ToArray())
    };

    private static JsonObject Seat(string id, object? x, object? y, string type = "free") => new()
    {
        ["id"] = id,
        ["x"] = JsonNode.Parse(JsonSerializer.Serialize(x)),
        ["y"] = JsonNode.Parse(JsonSerializer.Serialize(y)),
        ["type"] = type
    };

    private static JsonObject Assignments(params JsonObject[] assignments) => new()
    {
        ["assignments"] = new JsonArray(assignments)
    };

    private static JsonObject Assignment(string workstationId, string? roseta = null, string? deviceId = null, string? personId = null)
    {
        var assignment = new JsonObject { ["workstationId"] = workstationId };
        if (roseta is not null) assignment["roseta"] = roseta;
        if (deviceId is not null) assignment["deviceId"] = deviceId;
        if (personId is not null) assignment["personId"] = personId;
        return assignment;
    }

    private static ValidationResult Single(IEnumerable<ValidationResult> results, string ruleId) =>
        DomainTestSupport.Single(results, result => result.RuleId == ruleId);
}
