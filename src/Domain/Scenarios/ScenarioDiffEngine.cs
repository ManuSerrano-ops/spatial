using System.Text.Json.Nodes;

namespace PlanoOpenSpaceIT.Windows;

internal enum ScenarioChangeKind { Added, Removed, Moved, Modified }

internal sealed record ScenarioFieldChange(string Field, JsonNode? Before, JsonNode? After);

internal sealed record ScenarioOperation(
    string Id,
    string Type,
    bool Atomic,
    IReadOnlyList<string> Members);

internal sealed record ScenarioDiffChange(
    string Id,
    ScenarioChangeKind Kind,
    string EntityType,
    string EntityId,
    string? MapId,
    string? MapName,
    string? FromCell,
    string? ToCell,
    IReadOnlyList<ScenarioFieldChange> ChangedFields,
    JsonObject? Before,
    JsonObject? After,
    ScenarioOperation? Operation = null);

internal sealed record ScenarioImpactSummary(
    int Total,
    int Added,
    int Removed,
    int Moved,
    int Modified,
    int Assignments,
    int Workspaces,
    int ChangedFields,
    IReadOnlyDictionary<string, int> ByMap);

internal sealed record ScenarioValidationImpact(
    IReadOnlyList<ValidationResult> Introduced,
    IReadOnlyList<ValidationResult> Resolved,
    IReadOnlyList<ValidationResult> Persistent);

internal sealed record ScenarioComparison(
    IReadOnlyList<ScenarioDiffChange> Changes,
    ScenarioImpactSummary ImpactSummary,
    ScenarioValidationImpact ValidationImpact);

internal static class ScenarioDiffEngine
{
    private const int GridColumns = 24;
    private const int GridRows = 18;
    private static readonly HashSet<string> AuditFields = new(StringComparer.Ordinal) { "updatedAt", "updatedBy", "mapId", "mapName" };
    private static readonly HashSet<string> DerivedWorkspaceFields = new(StringComparer.Ordinal) { "gridCell", "id" };
    private static readonly HashSet<string> AssignmentIdentityFields = new(StringComparer.Ordinal) { "workstationId" };

    internal static ScenarioComparison Compare(
        JsonObject baseState,
        JsonObject draftState,
        IReadOnlyList<ValidationResult>? baseValidation = null,
        IReadOnlyList<ValidationResult>? draftValidation = null,
        IReadOnlyList<ScenarioOperation>? operations = null)
    {
        ArgumentNullException.ThrowIfNull(baseState);
        ArgumentNullException.ThrowIfNull(draftState);

        var baseAssignments = IndexAssignments(baseState);
        var draftAssignments = IndexAssignments(draftState);
        var baseSeats = IndexSeats(baseState);
        var draftSeats = IndexSeats(draftState);
        var changes = new List<ScenarioDiffChange>();

        foreach (var id in baseAssignments.Keys.Union(draftAssignments.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var before = baseAssignments.GetValueOrDefault(id);
            var after = draftAssignments.GetValueOrDefault(id);
            var fields = ChangedFields(before, after, isWorkspace: false);
            if (fields.Count == 0) continue;
            var seat = draftSeats.Values.Concat(baseSeats.Values).FirstOrDefault(item => item.Id == id);
            changes.Add(new ScenarioDiffChange(
                $"assignment|{id}", Kind(before, after, fields, isWorkspace: false), "assignment", id,
                seat?.MapId, seat?.MapName, null, null, fields, Meaningful(before, false), Meaningful(after, false)));
        }

        foreach (var key in baseSeats.Keys.Union(draftSeats.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var before = baseSeats.GetValueOrDefault(key);
            var after = draftSeats.GetValueOrDefault(key);
            var fields = ChangedFields(before?.Value, after?.Value, isWorkspace: true);
            if (fields.Count == 0) continue;
            var item = after ?? before!;
            changes.Add(new ScenarioDiffChange(
                $"seat|{key}", Kind(before?.Value, after?.Value, fields, isWorkspace: true), "workspace", item.Id,
                item.MapId, item.MapName, Cell(before?.Value), Cell(after?.Value), fields,
                Meaningful(before?.Value, true), Meaningful(after?.Value, true)));
        }

        var ordered = changes.OrderBy(change => change.Kind).ThenBy(change => change.EntityType, StringComparer.Ordinal).ThenBy(change => change.MapId, StringComparer.Ordinal).ThenBy(change => change.EntityId, StringComparer.Ordinal).ThenBy(change => change.Id, StringComparer.Ordinal).ToArray();
        var annotated = AttachOperations(ordered, operations ?? []);
        return new ScenarioComparison(annotated, Summary(annotated), ValidationImpact(baseValidation ?? [], draftValidation ?? []));
    }

    private static IReadOnlyList<ScenarioDiffChange> AttachOperations(IReadOnlyList<ScenarioDiffChange> changes, IReadOnlyList<ScenarioOperation> operations)
    {
        var operationByMember = operations
            .SelectMany(operation => operation.Members.Select(member => (Member: member, Operation: operation)))
            .GroupBy(item => item.Member, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().Operation, StringComparer.Ordinal);
        return changes.Select(change => operationByMember.TryGetValue(change.Id, out var operation) ? change with { Operation = operation } : change).ToArray();
    }

    private static ScenarioChangeKind Kind(JsonObject? before, JsonObject? after, IReadOnlyList<ScenarioFieldChange> fields, bool isWorkspace)
    {
        if (before is null) return ScenarioChangeKind.Added;
        if (after is null) return ScenarioChangeKind.Removed;
        return isWorkspace && fields.All(field => field.Field is "x" or "y") ? ScenarioChangeKind.Moved : ScenarioChangeKind.Modified;
    }

    private static IReadOnlyList<ScenarioFieldChange> ChangedFields(JsonObject? before, JsonObject? after, bool isWorkspace)
    {
        var beforeFields = Fields(before, isWorkspace);
        var afterFields = Fields(after, isWorkspace);
        return beforeFields.Keys.Union(afterFields.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Where(field => !JsonNode.DeepEquals(beforeFields.GetValueOrDefault(field), afterFields.GetValueOrDefault(field)))
            .Select(field => new ScenarioFieldChange(field, beforeFields.GetValueOrDefault(field)?.DeepClone(), afterFields.GetValueOrDefault(field)?.DeepClone()))
            .ToArray();
    }

    private static Dictionary<string, JsonNode?> Fields(JsonObject? item, bool isWorkspace) => item is null
        ? []
        : item.Where(pair => !AuditFields.Contains(pair.Key) && (!isWorkspace ? !AssignmentIdentityFields.Contains(pair.Key) : !DerivedWorkspaceFields.Contains(pair.Key))).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static JsonObject? Meaningful(JsonObject? item, bool isWorkspace)
    {
        if (item is null) return null;
        var copy = (JsonObject)item.DeepClone();
        foreach (var field in AuditFields) copy.Remove(field);
        if (isWorkspace) copy.Remove("gridCell");
        return copy;
    }

    private static ScenarioImpactSummary Summary(IReadOnlyList<ScenarioDiffChange> changes)
    {
        var byMap = changes.Where(change => !string.IsNullOrWhiteSpace(change.MapId)).GroupBy(change => change.MapId!, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return new ScenarioImpactSummary(
            changes.Count,
            changes.Count(change => change.Kind == ScenarioChangeKind.Added),
            changes.Count(change => change.Kind == ScenarioChangeKind.Removed),
            changes.Count(change => change.Kind == ScenarioChangeKind.Moved),
            changes.Count(change => change.Kind == ScenarioChangeKind.Modified),
            changes.Count(change => change.EntityType == "assignment"),
            changes.Count(change => change.EntityType == "workspace"),
            changes.Sum(change => change.ChangedFields.Count),
            byMap);
    }

    private static ScenarioValidationImpact ValidationImpact(IReadOnlyList<ValidationResult> baseResults, IReadOnlyList<ValidationResult> draftResults)
    {
        var before = baseResults.ToDictionary(result => result.Id, StringComparer.Ordinal);
        var after = draftResults.ToDictionary(result => result.Id, StringComparer.Ordinal);
        return new ScenarioValidationImpact(
            after.Keys.Except(before.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(id => after[id]).ToArray(),
            before.Keys.Except(after.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(id => before[id]).ToArray(),
            before.Keys.Intersect(after.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(id => after[id]).ToArray());
    }

    private static Dictionary<string, JsonObject> IndexAssignments(JsonObject state) => (state["assignments"]?["assignments"]?.AsArray().OfType<JsonObject>() ?? [])
        .ToDictionary(item => Text(item["workstationId"]), item => item, StringComparer.Ordinal);

    private static Dictionary<string, Seat> IndexSeats(JsonObject state) => (state["maps"]?["maps"]?.AsArray().OfType<JsonObject>() ?? [])
        .SelectMany(map => (map["seats"]?.AsArray().OfType<JsonObject>() ?? []).Select(seat => new Seat(Text(map["id"]), Text(map["name"]), Text(seat["id"]), seat)))
        .ToDictionary(seat => $"{seat.MapId}|{seat.Id}", seat => seat, StringComparer.Ordinal);

    private static string Cell(JsonObject? item)
    {
        if (!Coordinate(item?["x"], out var x) || !Coordinate(item?["y"], out var y)) return "";
        var column = Math.Clamp((int)Math.Floor(x * GridColumns), 0, GridColumns - 1);
        var row = Math.Clamp((int)Math.Floor(y * GridRows), 0, GridRows - 1);
        return $"{ColumnName(column)}-{row + 1:D2}";
    }

    private static string ColumnName(int column) { var result = ""; for (column++; column > 0; column = (column - 1) / 26) result = (char)('A' + (column - 1) % 26) + result; return result; }
    private static bool Coordinate(JsonNode? node, out double value)
    {
        value = 0;
        return node is JsonValue json && json.TryGetValue<double>(out value);
    }
    private static string Text(JsonNode? value) => value?.ToString() ?? "";
    private sealed record Seat(string MapId, string MapName, string Id, JsonObject Value);
}
