using System.Text.Json.Nodes;
using PlanoOpenSpaceIT.Windows;

var tests = new (string Name, Action Test)[]
{
    ("display location", DisplayLocation),
    ("valid proposal", ValidProposal),
    ("technical identifiers preserved", TechnicalIdentifiersPreserved),
    ("destination unavailable", DestinationUnavailable),
    ("duplicate destination", DuplicateDestination),
    ("source missing assignment", SourceMissingAssignment),
    ("missing endpoints", MissingEndpoints),
    ("related validation", RelatedValidation),
    ("related scenario diff", RelatedScenarioDiff),
    ("determinism", Determinism),
    ("scenario creation keeps reality unchanged", ScenarioCreationKeepsRealityUnchanged)
};
var passed = 0;
foreach (var (name, test) in tests) { try { test(); passed++; } catch (Exception exception) { Console.Error.WriteLine($"FAIL: {name}: {exception.Message}"); } }
Console.WriteLine($"MovementPlannerHarness: {passed}/{tests.Length} passed, {tests.Length - passed} failed");
return passed == tests.Length ? 0 : 1;

static void DisplayLocation()
{
    Equal("A-01", SpatialLocation.FromCoordinates(JsonValue.Create(0d), JsonValue.Create(0d)), "Origin cell.");
    Equal("X-18", SpatialLocation.FromCoordinates(JsonValue.Create(1d), JsonValue.Create(1d)), "Maximum cell.");
    Equal("M-10", SpatialLocation.FromCoordinates(JsonValue.Create(.5d), JsonValue.Create(.5d)), "Human-readable grid location.");
}

static void ValidProposal()
{
    var plan = Run([new("N-01", "N-02")]);
    Equal(1, plan.Proposals.Count, "One valid request is planned.");
    Equal("J-06", plan.Proposals[0].Source.DisplayLocation, "Source location is derived.");
    Equal("O-11", plan.Proposals[0].Destination.DisplayLocation, "Destination location is derived.");
}

static void TechnicalIdentifiersPreserved()
{
    var proposal = Run([new("N-01", "N-02")]).Proposals.Single();
    Equal("move|N-01|N-02", proposal.Id, "Proposal ID uses stable technical IDs.");
    Equal("N-01", proposal.Source.WorkspaceId, "Source technical ID is preserved.");
    Equal("N-02", proposal.Destination.WorkspaceId, "Destination technical ID is preserved.");
}

static void DestinationUnavailable() => Equal("destination-unavailable", Run([new("N-01", "N-03")], assignments: [Assignment("N-01"), Assignment("N-03")]).Issues.Single().Code, "Assigned destination is blocked.");
static void DuplicateDestination() => Equal("duplicate-destination", Run([new("N-01", "N-02"), new("N-03", "N-02")], assignments: [Assignment("N-01"), Assignment("N-03")]).Issues.Single().Code, "Duplicate destination is blocked.");
static void SourceMissingAssignment() => Equal("source-unassigned", Run([new("N-02", "N-03")]).Issues.Single().Code, "Unassigned source is blocked.");
static void MissingEndpoints()
{
    var issues = Run([new("missing", "N-02"), new("N-01", "missing")]).Issues.Select(issue => issue.Code).Order().ToArray();
    SequenceEqual(new[] { "destination-missing", "source-missing" }, issues, "Missing endpoints are explicit.");
}

static void RelatedValidation()
{
    var validation = new[] { new ValidationResult("rule|x|N-01,N-02", "rule", ValidationSeverity.Warning, "assignment", "x", "norte", null, "Title", "Message", null, ["N-01", "N-02"], "Review") };
    Equal(1, Run([new("N-01", "N-02")], validation: validation).Proposals.Single().RelatedProblems.Count, "Existing validation results are attached without revalidation.");
}

static void RelatedScenarioDiff()
{
    var changes = new[] { new ScenarioDiffChange("seat|norte|N-02", ScenarioChangeKind.Moved, "workspace", "N-02", "norte", "Norte", "J-06", "O-11", [], null, null) };
    Equal(1, Run([new("N-01", "N-02")], scenarioChanges: changes).Proposals.Single().RelatedScenarioChanges.Count, "Existing scenario changes are attached without recomputing the diff.");
}

static void Determinism()
{
    var first = Run([new("N-03", "N-02"), new("N-01", "N-02")], assignments: [Assignment("N-01"), Assignment("N-03")]);
    var second = Run([new("N-03", "N-02"), new("N-01", "N-02")], assignments: [Assignment("N-01"), Assignment("N-03")]);
    SequenceEqual(first.Issues.Select(issue => $"{issue.Id}|{issue.Code}"), second.Issues.Select(issue => $"{issue.Id}|{issue.Code}"), "Issues are stable.");
}

static void ScenarioCreationKeepsRealityUnchanged()
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
        Equal(before, File.ReadAllText(Path.Combine(data, "assignments.json")), "Reality assignments must remain byte-for-byte unchanged.");
        var effective = store.Load(scenarioId);
        var draftAssignments = effective["assignments"]?["assignments"]?.AsArray().OfType<JsonObject>().Select(item => item["workstationId"]?.GetValue<string>()).ToArray() ?? [];
        SequenceEqual(new[] { "N-02" }, draftAssignments!, "Scenario draft contains the planned move.");
        var diff = store.GetScenarioDiff(new JsonObject { ["scenarioId"] = scenarioId });
        var assignmentChanges = diff["changes"]?.AsArray().OfType<JsonObject>().Where(change => change["entityType"]?.GetValue<string>() == "assignment").ToArray() ?? [];
        Equal(2, assignmentChanges.Length, "Scenario diff contains the removed and added assignment identities.");
        Equal(0, store.RunValidation(scenarioId)["summary"]?["total"]?.GetValue<int>(), "Validation runs on the created effective scenario.");
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
static void Equal<T>(T expected, T? actual, string message) { if (!EqualityComparer<T>.Default.Equals(expected, actual!)) throw new InvalidOperationException($"{message} Expected {expected}; actual {actual}."); }
static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message) { if (!expected.SequenceEqual(actual)) throw new InvalidOperationException(message); }
