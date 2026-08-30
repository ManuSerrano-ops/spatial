using System.Text.Json.Nodes;

namespace PlanoOpenSpaceIT.Windows;

internal sealed record MovementRequest(string SourceWorkspaceId, string DestinationWorkspaceId);

internal sealed record MovementEndpoint(
    string WorkspaceId,
    string MapId,
    string DisplayLocation,
    string? PersonId,
    string? DeviceId,
    string? Roseta);

internal sealed record MovementPlanIssue(
    string Id,
    string Code,
    string Message,
    string SourceWorkspaceId,
    string? DestinationWorkspaceId);

internal sealed record MovementProposal(
    string Id,
    MovementEndpoint Source,
    MovementEndpoint Destination,
    IReadOnlyList<ValidationResult> RelatedProblems,
    IReadOnlyList<ScenarioDiffChange> RelatedScenarioChanges);

internal sealed record MovementPlanSummary(int Requested, int Planned, int Blocked);

internal sealed record MovementPlan(
    IReadOnlyList<MovementProposal> Proposals,
    IReadOnlyList<MovementPlanIssue> Issues,
    MovementPlanSummary Summary);

internal static class MovementPlanner
{
    internal static MovementPlan Run(
        JsonObject maps,
        JsonObject assignments,
        IEnumerable<MovementRequest> requests,
        IReadOnlyList<ValidationResult>? validationResults = null,
        IReadOnlyList<ScenarioDiffChange>? scenarioChanges = null)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(requests);

        var seats = (maps["maps"]?.AsArray().OfType<JsonObject>() ?? [])
            .SelectMany(map => (map["seats"]?.AsArray().OfType<JsonObject>() ?? []).Select(seat => new Seat(Text(map["id"]), Text(seat["id"]), seat)))
            .ToDictionary(seat => seat.WorkspaceId, StringComparer.Ordinal);
        var byWorkspace = (assignments["assignments"]?.AsArray().OfType<JsonObject>() ?? [])
            .ToDictionary(item => Text(item["workstationId"]), item => item, StringComparer.Ordinal);
        var validation = validationResults ?? [];
        var changes = scenarioChanges ?? [];
        var proposals = new List<MovementProposal>();
        var issues = new List<MovementPlanIssue>();
        var destinations = new HashSet<string>(StringComparer.Ordinal);
        var normalized = requests.Select(request => new MovementRequest(request.SourceWorkspaceId?.Trim() ?? "", request.DestinationWorkspaceId?.Trim() ?? ""))
            .OrderBy(request => request.SourceWorkspaceId, StringComparer.Ordinal).ThenBy(request => request.DestinationWorkspaceId, StringComparer.Ordinal).ToArray();

        foreach (var request in normalized)
        {
            var id = $"move|{request.SourceWorkspaceId}|{request.DestinationWorkspaceId}";
            if (!seats.TryGetValue(request.SourceWorkspaceId, out var source)) { issues.Add(Issue(id, "source-missing", "El puesto de origen no existe en el contexto activo.", request)); continue; }
            if (!seats.TryGetValue(request.DestinationWorkspaceId, out var destination)) { issues.Add(Issue(id, "destination-missing", "El puesto de destino no existe en el contexto activo.", request)); continue; }
            if (source.WorkspaceId == destination.WorkspaceId) { issues.Add(Issue(id, "same-workspace", "Origen y destino deben ser puestos distintos.", request)); continue; }
            byWorkspace.TryGetValue(source.WorkspaceId, out var sourceAssignment);
            if (sourceAssignment is null && !HasLegacyPerson(source)) { issues.Add(Issue(id, "source-unassigned", "El puesto de origen no tiene asignación para mover.", request)); continue; }
            if (!destinations.Add(destination.WorkspaceId)) { issues.Add(Issue(id, "duplicate-destination", "El destino se ha solicitado para más de un movimiento.", request)); continue; }
            if (byWorkspace.ContainsKey(destination.WorkspaceId) || HasLegacyPerson(destination)) { issues.Add(Issue(id, "destination-unavailable", "El destino ya tiene una asignación vigente o reservada.", request)); continue; }

            var sourceEndpoint = Endpoint(source, sourceAssignment);
            var destinationEndpoint = Endpoint(destination, null);
            var related = validation.Where(result => Affects(result, source.WorkspaceId) || Affects(result, destination.WorkspaceId)).OrderBy(result => result.Id, StringComparer.Ordinal).ToArray();
            var relatedChanges = changes.Where(change => change.EntityId == source.WorkspaceId || change.EntityId == destination.WorkspaceId).OrderBy(change => change.Id, StringComparer.Ordinal).ToArray();
            proposals.Add(new MovementProposal(id, sourceEndpoint, destinationEndpoint, related, relatedChanges));
        }

        var orderedIssues = issues.OrderBy(issue => issue.Code, StringComparer.Ordinal).ThenBy(issue => issue.SourceWorkspaceId, StringComparer.Ordinal).ThenBy(issue => issue.DestinationWorkspaceId, StringComparer.Ordinal).ToArray();
        var orderedProposals = proposals.OrderBy(proposal => proposal.Id, StringComparer.Ordinal).ToArray();
        return new MovementPlan(orderedProposals, orderedIssues, new MovementPlanSummary(normalized.Length, orderedProposals.Length, orderedIssues.Length));
    }

    private static MovementEndpoint Endpoint(Seat seat, JsonObject? assignment) => new(
        seat.WorkspaceId,
        seat.MapId,
        SpatialLocation.FromSeat(seat.Value),
        TextOrNull(assignment?["personId"]) ?? (assignment is null ? TextOrNull(seat.Value["personId"]) : null),
        TextOrNull(assignment?["deviceId"]),
        TextOrNull(assignment?["roseta"]) ?? (assignment is null ? TextOrNull(seat.Value["roseta"]) : null));

    private static bool HasLegacyPerson(Seat seat) => TextOrNull(seat.Value["personId"]) is not null;

    private static bool Affects(ValidationResult result, string workspaceId) =>
        (result.EntityType == "workspace" && result.EntityId == workspaceId) || result.RelatedEntityIds.Contains(workspaceId, StringComparer.Ordinal);

    private static MovementPlanIssue Issue(string id, string code, string message, MovementRequest request) => new(id, code, message, request.SourceWorkspaceId, request.DestinationWorkspaceId);
    private static string Text(JsonNode? node) => node?.ToString() ?? "";
    private static string? TextOrNull(JsonNode? node) { var value = Text(node); return value.Length == 0 ? null : value; }
    private sealed record Seat(string MapId, string WorkspaceId, JsonObject Value);
}
