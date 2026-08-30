using System.Text.Json.Nodes;

namespace PlanoOpenSpaceIT.Windows;

internal static class IntegrityReport
{
    internal static JsonObject Build(JsonObject maps, JsonObject assignments, JsonObject positions)
    {
        var seats = maps["maps"]?.AsArray().OfType<JsonObject>()
            .SelectMany(map => map["seats"]?.AsArray().OfType<JsonObject>().Select(seat => new Seat(Text(map["id"]), Text(seat["id"]), Text(seat["type"]))) ?? [])
            .ToArray() ?? [];
        var seatKeys = seats.Select(seat => (seat.MapId, seat.Id)).ToHashSet();
        var seatIds = seats.Select(seat => seat.Id).ToHashSet(StringComparer.Ordinal);
        var allAssignments = assignments["assignments"]?.AsArray().OfType<JsonObject>().ToArray() ?? [];
        var assignmentsBySeat = allAssignments
            .GroupBy(item => Text(item["workstationId"]), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var missingWorkstations = allAssignments.Where(item => !seatIds.Contains(Text(item["workstationId"]))).Select(item => Text(item["workstationId"])).Distinct(StringComparer.Ordinal).Order().ToArray();
        var historicalOccupiedMarks = seats.Where(seat => string.Equals(seat.Type, "occupied", StringComparison.OrdinalIgnoreCase) && !assignmentsBySeat.ContainsKey(seat.Id)).Select(seat => seat.Id).Order().ToArray();
        var duplicatedRosetas = allAssignments
            .Where(item => seatIds.Contains(Text(item["workstationId"])))
            .Select(item => new { Roseta = Text(item["roseta"]).Trim(), WorkstationId = Text(item["workstationId"]) })
            .Where(item => item.Roseta.Length > 0)
            .GroupBy(item => item.Roseta, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.WorkstationId).Distinct(StringComparer.Ordinal).Count() > 1)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new JsonObject
            {
                ["roseta"] = group.Key,
                ["workstationIds"] = Array(group.Select(item => item.WorkstationId).Distinct(StringComparer.Ordinal).Order())
            }).ToArray();
        var orphanPositions = positions["positions"]?.AsArray().OfType<JsonObject>()
            .Where(position => !seatKeys.Contains((Text(position["mapId"]), Text(position["seatId"]))))
            .Select(position => new JsonObject { ["mapId"] = Text(position["mapId"]), ["seatId"] = Text(position["seatId"]) })
            .ToArray() ?? [];

        return new JsonObject
        {
            ["generatedAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["duplicateRosetas"] = new JsonArray(duplicatedRosetas),
            ["historicalOccupiedMarksWithoutAssignment"] = Array(historicalOccupiedMarks),
            ["assignmentsWithMissingWorkstation"] = Array(missingWorkstations),
            ["orphanPositions"] = new JsonArray(orphanPositions),
            ["counts"] = new JsonObject
            {
                ["duplicateRosetas"] = duplicatedRosetas.Length,
                ["historicalOccupiedMarksWithoutAssignment"] = historicalOccupiedMarks.Length,
                ["assignmentsWithMissingWorkstation"] = missingWorkstations.Length,
                ["orphanPositions"] = orphanPositions.Length
            }
        };
    }


    private static JsonArray Array(IEnumerable<string> values) => new(values.Select(value => JsonValue.Create(value)).ToArray());

    private static string Text(JsonNode? node) => node?.GetValue<string>() ?? "";

    private sealed record Seat(string MapId, string Id, string Type);
}
