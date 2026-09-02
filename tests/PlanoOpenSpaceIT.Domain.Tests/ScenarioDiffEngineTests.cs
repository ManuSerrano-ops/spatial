using System.Text.Json.Nodes;
using PlanoOpenSpaceIT.Windows;
using Xunit;

namespace PlanoOpenSpaceIT.Domain.Tests;

public sealed class ScenarioDiffEngineTests
{
    [Fact]
    public void UnchangedState()
    {
        var state = State();

        Assert.Empty(ScenarioDiffEngine.Compare(state, state).Changes);
    }

    [Fact]
    public void AssignmentAdded()
    {
        var comparison = ScenarioDiffEngine.Compare(State(), State(assignments: [Assignment("A", roseta: "R1")]));
        var change = Single(comparison, "assignment|A");

        Assert.Equal(ScenarioChangeKind.Added, change.Kind);
        Assert.Equal("roseta", change.ChangedFields.Single().Field);
    }

    [Fact]
    public void AssignmentRemoved()
    {
        var comparison = ScenarioDiffEngine.Compare(State(assignments: [Assignment("A", roseta: "R1")]), State());

        Assert.Equal(ScenarioChangeKind.Removed, Single(comparison, "assignment|A").Kind);
    }

    [Fact]
    public void AssignmentModifiedFields()
    {
        var baseState = State(assignments: [Assignment("A", roseta: "R1", personId: "P1", updatedAt: "old")]);
        var draft = State(assignments: [Assignment("A", roseta: "R2", personId: "P1", updatedAt: "new")]);
        var change = Single(ScenarioDiffEngine.Compare(baseState, draft), "assignment|A");

        Assert.Equal(ScenarioChangeKind.Modified, change.Kind);
        DomainTestSupport.SequenceEqual(["roseta"], change.ChangedFields.Select(field => field.Field), "Audit fields must be ignored.");
        Assert.Equal("R1", change.ChangedFields[0].Before!.GetValue<string>());
        Assert.Equal("R2", change.ChangedFields[0].After!.GetValue<string>());
    }

    [Fact]
    public void WorkspaceAddedAndRemoved()
    {
        var added = Single(ScenarioDiffEngine.Compare(State(), State(seats: [Seat("A"), Seat("B")])), "seat|norte|B");
        var removed = Single(ScenarioDiffEngine.Compare(State(seats: [Seat("A"), Seat("B")]), State()), "seat|norte|B");

        Assert.Equal(ScenarioChangeKind.Added, added.Kind);
        Assert.Equal(ScenarioChangeKind.Removed, removed.Kind);
    }

    [Fact]
    public void WorkspaceMoved()
    {
        var comparison = ScenarioDiffEngine.Compare(State(seats: [Seat("A", .1, .1)]), State(seats: [Seat("A", .7, .4)]));
        var change = Single(comparison, "seat|norte|A");

        Assert.Equal(ScenarioChangeKind.Moved, change.Kind);
        DomainTestSupport.SequenceEqual(["x", "y"], change.ChangedFields.Select(field => field.Field), "Movement retains coordinate changes.");
        Assert.True(change.FromCell != change.ToCell, "Movement must expose cells.");
    }

    [Fact]
    public void WorkspaceMovedAndModified()
    {
        var comparison = ScenarioDiffEngine.Compare(State(seats: [Seat("A", .1, .1, "Mesa A")]), State(seats: [Seat("A", .7, .4, "Mesa B")]));
        var change = Single(comparison, "seat|norte|A");

        Assert.Equal(ScenarioChangeKind.Modified, change.Kind);
        DomainTestSupport.SequenceEqual(["name", "x", "y"], change.ChangedFields.Select(field => field.Field), "All operational fields must be retained.");
    }

    [Fact]
    public void DeterministicOrder()
    {
        var baseState = State(assignments: [Assignment("B", roseta: "R1")], seats: [Seat("A"), Seat("B")]);
        var draft = State(assignments: [Assignment("A", roseta: "R2")], seats: [Seat("A", .7, .4), Seat("C")]);
        var first = ScenarioDiffEngine.Compare(baseState, draft);
        var second = ScenarioDiffEngine.Compare(baseState, draft);

        DomainTestSupport.SequenceEqual(
            first.Changes.Select(change => $"{change.Id}|{change.Kind}|{string.Join(',', change.ChangedFields.Select(field => field.Field))}"),
            second.Changes.Select(change => $"{change.Id}|{change.Kind}|{string.Join(',', change.ChangedFields.Select(field => field.Field))}"),
            "Same states must preserve change IDs, kinds and order.");
    }

    [Fact]
    public void ImpactSummary()
    {
        var comparison = ScenarioDiffEngine.Compare(
            State(assignments: [Assignment("A", roseta: "R1")], seats: [Seat("A"), Seat("B")]),
            State(assignments: [Assignment("A", roseta: "R2"), Assignment("B", roseta: "R3")], seats: [Seat("A", .6, .4), Seat("C")]));
        var summary = comparison.ImpactSummary;

        Assert.Equal(5, summary.Total);
        Assert.Equal(2, summary.Added);
        Assert.Equal(1, summary.Removed);
        Assert.Equal(1, summary.Moved);
        Assert.Equal(1, summary.Modified);
        Assert.Equal(5, summary.ByMap["norte"]);
    }

    [Fact]
    public void ValidationImpact()
    {
        var baseState = State(seats: [Seat("A"), Seat("B")]);
        var draftState = State(assignments: [Assignment("A", roseta: "R1"), Assignment("B", roseta: "R1")], seats: [Seat("A"), Seat("B")]);
        var clean = ValidationEngine.Run(baseState["maps"]!.AsObject(), baseState["assignments"]!.AsObject());
        var invalid = ValidationEngine.Run(draftState["maps"]!.AsObject(), draftState["assignments"]!.AsObject());
        var comparison = ScenarioDiffEngine.Compare(baseState, draftState, clean, invalid);

        var introduced = Assert.Single(comparison.ValidationImpact.Introduced);
        Assert.Equal("duplicate-network-outlet", introduced.RuleId);
        Assert.Empty(comparison.ValidationImpact.Resolved);
    }

    private static ScenarioDiffChange Single(ScenarioComparison comparison, string id) =>
        DomainTestSupport.Single(comparison.Changes, change => change.Id == id);

    private static JsonObject State(JsonObject[]? assignments = null, JsonObject[]? seats = null) => new()
    {
        ["maps"] = new JsonObject { ["maps"] = new JsonArray(new JsonObject { ["id"] = "norte", ["name"] = "Norte", ["seats"] = new JsonArray(seats ?? [Seat("A")]) }) },
        ["assignments"] = new JsonObject { ["assignments"] = new JsonArray(assignments ?? []) }
    };

    private static JsonObject Seat(string id, double x = .1, double y = .1, string name = "Mesa") => new()
    {
        ["id"] = id, ["name"] = name, ["type"] = "free", ["x"] = x, ["y"] = y, ["gridCell"] = "ignored", ["updatedAt"] = "audit"
    };

    private static JsonObject Assignment(string workstationId, string? roseta = null, string? personId = null, string? updatedAt = null)
    {
        var assignment = new JsonObject { ["workstationId"] = workstationId };
        if (roseta is not null) assignment["roseta"] = roseta;
        if (personId is not null) assignment["personId"] = personId;
        if (updatedAt is not null) assignment["updatedAt"] = updatedAt;
        return assignment;
    }
}
