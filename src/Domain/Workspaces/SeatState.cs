using System.Text.Json.Nodes;

namespace PlanoOpenSpaceIT.Windows;

internal enum SeatState
{
    Free,
    Occupied,
    Reserved
}

internal enum AssignmentCompleteness
{
    Complete,
    Partial
}

internal enum WorkspaceStateMode
{
    Automatic,
    Manual
}

internal sealed record EffectiveWorkspaceState(SeatState State, WorkspaceStateMode Mode, string? CurrentPersonId)
{
    internal bool HasCurrentAssignment => !string.IsNullOrWhiteSpace(CurrentPersonId) || State == SeatState.Reserved;
}

internal static class SeatStates
{
    internal static EffectiveWorkspaceState DeriveEffectiveWorkspaceState(JsonObject? seat, JsonObject? assignment)
    {
        var configured = Text(assignment?["configuredState"]);
        if (configured.Length == 0) configured = Text(assignment?["status"]);
        configured = configured.ToLowerInvariant();
        if (configured is "reserved" or "manual-reserved") return new EffectiveWorkspaceState(SeatState.Reserved, WorkspaceStateMode.Manual, PersonId(seat, assignment));
        if (configured is "free" or "manual-free") return new EffectiveWorkspaceState(SeatState.Free, WorkspaceStateMode.Manual, PersonId(seat, assignment));
        if (configured is "occupied" or "manual-occupied") return new EffectiveWorkspaceState(SeatState.Occupied, WorkspaceStateMode.Manual, PersonId(seat, assignment));
        var personId = PersonId(seat, assignment);
        return new EffectiveWorkspaceState(string.IsNullOrWhiteSpace(personId) ? SeatState.Free : SeatState.Occupied, WorkspaceStateMode.Automatic, personId);
    }

    internal static SeatState Derive(JsonObject? assignment, JsonObject? seat = null) => DeriveEffectiveWorkspaceState(seat, assignment).State;

    internal static AssignmentCompleteness Completeness(JsonObject? assignment, JsonObject? seat = null)
    {
        var effective = DeriveEffectiveWorkspaceState(seat, assignment);
        if (effective.State != SeatState.Occupied) return AssignmentCompleteness.Complete;
        var values = new[]
        {
            effective.CurrentPersonId,
            Text(assignment?["deviceId"]) is { Length: > 0 } device ? device : Text(seat?["deviceId"]) is { Length: > 0 } fallbackDevice ? fallbackDevice : Text(seat?["deviceName"]),
            Text(assignment?["locationId"]) is { Length: > 0 } location ? location : Text(seat?["location"]),
            Text(assignment?["roseta"]) is { Length: > 0 } outlet ? outlet : Text(seat?["roseta"])
        };
        return values.All(value => !string.IsNullOrWhiteSpace(value)) ? AssignmentCompleteness.Complete : AssignmentCompleteness.Partial;
    }

    internal static string WireName(SeatState state) => state.ToString().ToLowerInvariant();
    internal static string WireName(WorkspaceStateMode mode) => mode.ToString().ToLowerInvariant();

    internal static string WireCompleteness(AssignmentCompleteness completeness) => completeness.ToString().ToLowerInvariant();

    private static string? PersonId(JsonObject? seat, JsonObject? assignment)
    {
        var value = Text(assignment?["personId"]);
        return value.Length > 0 ? value : Text(seat?["personId"]) is { Length: > 0 } fallback ? fallback : null;
    }

    private static string Text(JsonNode? value) => value?.GetValue<string>() ?? "";
}