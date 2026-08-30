using System.Text.Json.Nodes;
using PlanoOpenSpaceIT.Windows;

var tests = new (string Name, Action Test)[]
{
    ("unchanged state", UnchangedState),
    ("assignment added", AssignmentAdded),
    ("assignment removed", AssignmentRemoved),
    ("assignment modified fields", AssignmentModifiedFields),
    ("workspace added and removed", WorkspaceAddedAndRemoved),
    ("workspace moved", WorkspaceMoved),
    ("workspace moved and modified", WorkspaceMovedAndModified),
    ("deterministic order", DeterministicOrder),
    ("impact summary", ImpactSummary),
    ("validation impact", ValidationImpact)
};

var passed = 0;
foreach (var (name, test) in tests)
{
    try { test(); passed++; }
    catch (Exception exception) { Console.Error.WriteLine($"FAIL: {name}: {exception.Message}"); }
}
Console.WriteLine($"ScenarioDiffEngineHarness: {passed}/{tests.Length} passed, {tests.Length - passed} failed");
return passed == tests.Length ? 0 : 1;

static void UnchangedState()
{
    var state = State();
    Equal(0, ScenarioDiffEngine.Compare(state, state).Changes.Count, "Unchanged state must produce no changes.");
}

static void AssignmentAdded()
{
    var comparison = ScenarioDiffEngine.Compare(State(), State(assignments: [Assignment("A", roseta: "R1")]));
    var change = Single(comparison, "assignment|A");
    Equal(ScenarioChangeKind.Added, change.Kind, "New assignment must be ADDED.");
    Equal("roseta", change.ChangedFields.Single().Field, "Added assignment exposes its field.");
}

static void AssignmentRemoved()
{
    var comparison = ScenarioDiffEngine.Compare(State(assignments: [Assignment("A", roseta: "R1")]), State());
    Equal(ScenarioChangeKind.Removed, Single(comparison, "assignment|A").Kind, "Removed assignment must be REMOVED.");
}

static void AssignmentModifiedFields()
{
    var baseState = State(assignments: [Assignment("A", roseta: "R1", personId: "P1", updatedAt: "old")]);
    var draft = State(assignments: [Assignment("A", roseta: "R2", personId: "P1", updatedAt: "new")]);
    var change = Single(ScenarioDiffEngine.Compare(baseState, draft), "assignment|A");
    Equal(ScenarioChangeKind.Modified, change.Kind, "Changed assignment must be MODIFIED.");
    SequenceEqual(new[] { "roseta" }, change.ChangedFields.Select(field => field.Field), "Audit fields must be ignored.");
    Equal("R1", change.ChangedFields[0].Before!.GetValue<string>(), "Before must be retained.");
    Equal("R2", change.ChangedFields[0].After!.GetValue<string>(), "After must be retained.");
}

static void WorkspaceAddedAndRemoved()
{
    var added = Single(ScenarioDiffEngine.Compare(State(), State(seats: [Seat("A"), Seat("B")])), "seat|norte|B");
    var removed = Single(ScenarioDiffEngine.Compare(State(seats: [Seat("A"), Seat("B")]), State()), "seat|norte|B");
    Equal(ScenarioChangeKind.Added, added.Kind, "New workspace must be ADDED.");
    Equal(ScenarioChangeKind.Removed, removed.Kind, "Removed workspace must be REMOVED.");
}

static void WorkspaceMoved()
{
    var comparison = ScenarioDiffEngine.Compare(State(seats: [Seat("A", .1, .1)]), State(seats: [Seat("A", .7, .4)]));
    var change = Single(comparison, "seat|norte|A");
    Equal(ScenarioChangeKind.Moved, change.Kind, "Coordinate-only workspace change must be MOVED.");
    SequenceEqual(new[] { "x", "y" }, change.ChangedFields.Select(field => field.Field), "Movement retains coordinate changes.");
    Assert(change.FromCell != change.ToCell, "Movement must expose cells.");
}

static void WorkspaceMovedAndModified()
{
    var comparison = ScenarioDiffEngine.Compare(State(seats: [Seat("A", .1, .1, "Mesa A")]), State(seats: [Seat("A", .7, .4, "Mesa B")]));
    var change = Single(comparison, "seat|norte|A");
    Equal(ScenarioChangeKind.Modified, change.Kind, "Movement with other field changes must be MODIFIED as one applicable change.");
    SequenceEqual(new[] { "name", "x", "y" }, change.ChangedFields.Select(field => field.Field), "All operational fields must be retained.");
}

static void DeterministicOrder()
{
    var baseState = State(assignments: [Assignment("B", roseta: "R1")], seats: [Seat("A"), Seat("B")]);
    var draft = State(assignments: [Assignment("A", roseta: "R2")], seats: [Seat("A", .7, .4), Seat("C")]);
    var first = ScenarioDiffEngine.Compare(baseState, draft);
    var second = ScenarioDiffEngine.Compare(baseState, draft);
    SequenceEqual(first.Changes.Select(change => $"{change.Id}|{change.Kind}|{string.Join(',', change.ChangedFields.Select(field => field.Field))}"), second.Changes.Select(change => $"{change.Id}|{change.Kind}|{string.Join(',', change.ChangedFields.Select(field => field.Field))}"), "Same states must preserve change IDs, kinds and order.");
}

static void ImpactSummary()
{
    var comparison = ScenarioDiffEngine.Compare(
        State(assignments: [Assignment("A", roseta: "R1")], seats: [Seat("A"), Seat("B")]),
        State(assignments: [Assignment("A", roseta: "R2"), Assignment("B", roseta: "R3")], seats: [Seat("A", .6, .4), Seat("C")]));
    var summary = comparison.ImpactSummary;
    Equal(5, summary.Total, "Expected two assignment and three workspace changes.");
    Equal(2, summary.Added, "Assignment and workspace additions are counted.");
    Equal(1, summary.Removed, "Workspace removal is counted.");
    Equal(1, summary.Moved, "Workspace movement is counted.");
    Equal(1, summary.Modified, "Assignment modification is counted.");
    Equal(5, summary.ByMap["norte"], "Map impact is aggregated.");
}

static void ValidationImpact()
{
    var baseState = State(seats: [Seat("A"), Seat("B")]);
    var draftState = State(assignments: [Assignment("A", roseta: "R1"), Assignment("B", roseta: "R1")], seats: [Seat("A"), Seat("B")]);
    var clean = ValidationEngine.Run(baseState["maps"]!.AsObject(), baseState["assignments"]!.AsObject());
    var invalid = ValidationEngine.Run(draftState["maps"]!.AsObject(), draftState["assignments"]!.AsObject());
    var comparison = ScenarioDiffEngine.Compare(baseState, draftState, clean, invalid);
    Equal(1, comparison.ValidationImpact.Introduced.Count, "Scenario conflict must be introduced.");
    Equal("duplicate-network-outlet", comparison.ValidationImpact.Introduced[0].RuleId, "Validation impact retains rule ID.");
    Equal(0, comparison.ValidationImpact.Resolved.Count, "No base issue is resolved.");
}

static ScenarioDiffChange Single(ScenarioComparison comparison, string id)
{
    var matches = comparison.Changes.Where(change => change.Id == id).ToArray();
    Equal(1, matches.Length, $"Expected one change with ID {id}.");
    return matches[0];
}

static JsonObject State(JsonObject[]? assignments = null, JsonObject[]? seats = null) => new()
{
    ["maps"] = new JsonObject { ["maps"] = new JsonArray(new JsonObject { ["id"] = "norte", ["name"] = "Norte", ["seats"] = new JsonArray(seats ?? [Seat("A")]) }) },
    ["assignments"] = new JsonObject { ["assignments"] = new JsonArray(assignments ?? []) }
};

static JsonObject Seat(string id, double x = .1, double y = .1, string name = "Mesa") => new()
{
    ["id"] = id, ["name"] = name, ["type"] = "free", ["x"] = x, ["y"] = y, ["gridCell"] = "ignored", ["updatedAt"] = "audit"
};

static JsonObject Assignment(string workstationId, string? roseta = null, string? personId = null, string? updatedAt = null)
{
    var assignment = new JsonObject { ["workstationId"] = workstationId };
    if (roseta is not null) assignment["roseta"] = roseta;
    if (personId is not null) assignment["personId"] = personId;
    if (updatedAt is not null) assignment["updatedAt"] = updatedAt;
    return assignment;
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

static void Assert(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}
