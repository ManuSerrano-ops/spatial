using System.Text.Json.Nodes;
using PlanoOpenSpaceIT.Windows;
using Xunit;

namespace PlanoOpenSpaceIT.Desktop.Tests;

public sealed class MovementPlannerTests
{

[Fact]
    public void DisplayLocation()
{
    Xunit.Assert.Equal("A-01", SpatialLocation.FromCoordinates(JsonValue.Create(0d), JsonValue.Create(0d)));
    Xunit.Assert.Equal("X-18", SpatialLocation.FromCoordinates(JsonValue.Create(1d), JsonValue.Create(1d)));
    Xunit.Assert.Equal("M-10", SpatialLocation.FromCoordinates(JsonValue.Create(.5d), JsonValue.Create(.5d)));
}

[Fact]
    public void ValidProposal()
{
    var plan = Run([new("N-01", "N-02")]);
    Xunit.Assert.Single(plan.Proposals);
    Xunit.Assert.Equal("J-06", plan.Proposals[0].Source.DisplayLocation);
    Xunit.Assert.Equal("O-11", plan.Proposals[0].Destination.DisplayLocation);
}

[Fact]
    public void TechnicalIdentifiersPreserved()
{
    var proposal = Run([new("N-01", "N-02")]).Proposals.Single();
    Xunit.Assert.Equal("move|N-01|N-02", proposal.Id);
    Xunit.Assert.Equal("N-01", proposal.Source.WorkspaceId);
    Xunit.Assert.Equal("N-02", proposal.Destination.WorkspaceId);
}

[Fact]
    public void DestinationUnavailable() => Xunit.Assert.Equal("destination-unavailable", Run([new("N-01", "N-03")], assignments: [Assignment("N-01"), Assignment("N-03")]).Issues.Single().Code);
[Fact]
    public void DuplicateDestination() => Xunit.Assert.Equal("duplicate-destination", Run([new("N-01", "N-02"), new("N-03", "N-02")], assignments: [Assignment("N-01"), Assignment("N-03")]).Issues.Single().Code);
[Fact]
    public void SourceMissingAssignment() => Xunit.Assert.Equal("source-unassigned", Run([new("N-02", "N-03")]).Issues.Single().Code);
[Fact]
    public void MissingEndpoints()
{
    var issues = Run([new("missing", "N-02"), new("N-01", "missing")]).Issues.Select(issue => issue.Code).Order().ToArray();
    Xunit.Assert.Equal(new[] { "destination-missing", "source-missing" }, issues);
}

[Fact]
    public void RelatedValidation()
{
    var validation = new[] { new ValidationResult("rule|x|N-01,N-02", "rule", ValidationSeverity.Warning, "assignment", "x", "norte", null, "Title", "Message", null, ["N-01", "N-02"], "Review") };
    Xunit.Assert.Single(Run([new("N-01", "N-02")], validation: validation).Proposals.Single().RelatedProblems);
}

[Fact]
    public void RelatedScenarioDiff()
{
    var changes = new[] { new ScenarioDiffChange("seat|norte|N-02", ScenarioChangeKind.Moved, "workspace", "N-02", "norte", "Norte", "J-06", "O-11", [], null, null) };
    Xunit.Assert.Single(Run([new("N-01", "N-02")], scenarioChanges: changes).Proposals.Single().RelatedScenarioChanges);
}

[Fact]
    public void Determinism()
{
    var first = Run([new("N-03", "N-02"), new("N-01", "N-02")], assignments: [Assignment("N-01"), Assignment("N-03")]);
    var second = Run([new("N-03", "N-02"), new("N-01", "N-02")], assignments: [Assignment("N-01"), Assignment("N-03")]);
    Xunit.Assert.Equal(first.Issues.Select(issue => $"{issue.Id}|{issue.Code}"), second.Issues.Select(issue => $"{issue.Id}|{issue.Code}"));
}

[Fact]
    public void ScenarioCreationKeepsRealityUnchanged()
{
    var root = Path.Combine(Path.GetTempPath(), "planner-harness-" + Guid.NewGuid().ToString("N"));
    var data = Path.Combine(root, "data");
    Directory.CreateDirectory(data);
    try
    {
        Write("maps.json", new JsonObject { ["maps"] = new JsonArray(new JsonObject { ["id"] = "norte", ["name"] = "Norte", ["seats"] = new JsonArray(Seat("N-01", .4, .3), Seat("N-02", .6, .6)) }) });
        Write("assignments.json", new JsonObject { ["version"] = 0, ["assignments"] = new JsonArray(Assignment("N-01")) });
        Write("positions.json", new JsonObject { ["positions"] = new JsonArray(new JsonObject { ["mapId"] = "norte", ["seatId"] = "N-01", ["x"] = .4, ["y"] = .3 }, new JsonObject { ["mapId"] = "norte", ["seatId"] = "N-02", ["x"] = .6, ["y"] = .6 }) });
        Write("events.json", new JsonObject { ["events"] = new JsonArray() });
        Write("people.json", new JsonObject { ["people"] = new JsonArray() });
        Write("devices.json", new JsonObject { ["devices"] = new JsonArray() });
        Write("locations.json", new JsonObject { ["locations"] = new JsonArray() });
        Write("state.json", new JsonObject { ["revision"] = 0 });
        var before = File.ReadAllText(Path.Combine(data, "assignments.json"));
        var store = DataStore.FromConfig(new AppConfig { NetworkRoot = root, DataFolder = "data", BackupFolder = "backups", LogsFolder = "logs", BackupRetentionMode = "disabled" });
        var created = new WebViewBridge(store).Dispatch("createScenarioFromMovementPlan", new JsonObject { ["name"] = "Plan aislado", ["requests"] = new JsonArray(new JsonObject { ["sourceWorkspaceId"] = "N-01", ["destinationWorkspaceId"] = "N-02" }) }).AsObject();
        var scenarioId = created["scenarioId"]?.GetValue<string>() ?? throw new InvalidOperationException("No scenario ID returned.");
        Xunit.Assert.Equal(before, File.ReadAllText(Path.Combine(data, "assignments.json")));
        var effective = store.Load(scenarioId);
        var draftAssignments = effective["assignments"]?["assignments"]?.AsArray().OfType<JsonObject>().Select(item => item["workstationId"]?.GetValue<string>()).ToArray() ?? [];
        Xunit.Assert.Equal(new[] { "N-02" }, draftAssignments!);
        var diff = store.GetScenarioDiff(new JsonObject { ["scenarioId"] = scenarioId });
        var assignmentChanges = diff["changes"]?.AsArray().OfType<JsonObject>().Where(change => change["entityType"]?.GetValue<string>() == "assignment").ToArray() ?? [];
        Xunit.Assert.Equal(2, assignmentChanges.Length);
        Xunit.Assert.Equal(0, store.RunValidation(scenarioId)["summary"]?["total"]?.GetValue<int>());
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }

    void Write(string name, JsonObject value) => File.WriteAllText(Path.Combine(data, name), value.ToJsonString());
}

static MovementPlan Run(IEnumerable<MovementRequest> requests, JsonObject[]? assignments = null, IReadOnlyList<ValidationResult>? validation = null, IReadOnlyList<ScenarioDiffChange>? scenarioChanges = null)
{
    var maps = new JsonObject { ["maps"] = new JsonArray(new JsonObject { ["id"] = "norte", ["seats"] = new JsonArray(Seat("N-01", .4, .3), Seat("N-02", .6, .6), Seat("N-03", .8, .7)) }) };
    var assignmentDocument = new JsonObject { ["assignments"] = new JsonArray(assignments ?? [Assignment("N-01")]) };
    return MovementPlanner.Run(maps, assignmentDocument, requests, validation, scenarioChanges);
}

static JsonObject Seat(string id, double x, double y) => new() { ["id"] = id, ["x"] = x, ["y"] = y };
static JsonObject Assignment(string workstationId) => new() { ["workstationId"] = workstationId, ["personId"] = "person" };

}
