using System.Text.Json.Nodes;

namespace PlanoOpenSpaceIT.Windows;

// Rates are percentages in the inclusive range 0..100, rounded to two decimal places.
internal sealed record SpatialSeatMetrics(
    int Total,
    int Occupied,
    int Free,
    int Reserved,
    decimal OccupancyRate,
    decimal AvailabilityRate);

internal sealed record SpatialValidationTotals(int Critical, int Warning, int Info)
{
    internal int Total => Critical + Warning + Info;
}

internal sealed record SpatialMapMetrics(
    string MapId,
    string? MapName,
    SpatialSeatMetrics Seats,
    SpatialValidationTotals Validation);

// Layer is one of occupancy, availability, problems, or scenario-changes.
// Point values are deliberately simple: occupied/free = 1; critical/warning/info = 3/2/1;
// and each scenario change = 1.
internal sealed record SpatialHeatmapPoint(
    string MapId,
    string? MapName,
    string SeatId,
    decimal X,
    decimal Y,
    string Layer,
    int Value,
    string? SourceId);

internal sealed record SpatialScenarioMetrics(int TotalChanges, int MappedChanges);

internal sealed record SpatialAnalyticsReport(
    SpatialSeatMetrics Totals,
    SpatialValidationTotals Validation,
    IReadOnlyList<SpatialMapMetrics> Maps,
    IReadOnlyList<SpatialHeatmapPoint> HeatmapPoints,
    SpatialScenarioMetrics? Scenario);

/// <summary>
/// Produces a deterministic, read-only analytics view from the supplied effective maps and assignments.
/// Map totals include every seat; heatmap layers include only seats with finite normalized coordinates.
/// </summary>
internal static class SpatialAnalyticsEngine
{
    private const string OccupancyLayer = "occupancy";
    private const string AvailabilityLayer = "availability";
    private const string ProblemsLayer = "problems";
    private const string ScenarioChangesLayer = "scenario-changes";

    internal static SpatialAnalyticsReport Analyze(
        JsonObject maps,
        JsonObject assignments,
        IReadOnlyList<ValidationResult>? validationResults = null,
        IReadOnlyList<ScenarioDiffChange>? scenarioChanges = null)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(assignments);

        var seats = ReadSeats(maps, assignments);
        var validation = ValidationEngine.OperationalResults(validationResults ?? []);
        var orderedMaps = (maps["maps"]?.AsArray().OfType<JsonObject>() ?? [])
            .Select(map => new MapKey(Text(map["id"]), TextOrNull(map["name"])))
            .OrderBy(map => map.MapId, StringComparer.Ordinal)
            .ThenBy(map => map.MapName, StringComparer.Ordinal)
            .Select(map => new SpatialMapMetrics(
                map.MapId,
                map.MapName,
                Metrics(seats.Where(seat => string.Equals(seat.MapId, map.MapId, StringComparison.Ordinal))),
                ValidationTotals(validation.Where(result => string.Equals(result.MapId, map.MapId, StringComparison.Ordinal)))))
            .ToArray();

        var points = new List<SpatialHeatmapPoint>();
        AddSeatStatePoints(seats, points);
        AddProblemPoints(seats, validation, points);
        var scenario = scenarioChanges is null
            ? null
            : AddScenarioPoints(seats, scenarioChanges, points);

        var orderedPoints = points
            .OrderBy(point => point.MapId, StringComparer.Ordinal)
            .ThenBy(point => point.MapName, StringComparer.Ordinal)
            .ThenBy(point => LayerOrder(point.Layer))
            .ThenBy(point => point.SeatId, StringComparer.Ordinal)
            .ThenBy(point => point.SourceId, StringComparer.Ordinal)
            .ThenBy(point => point.X)
            .ThenBy(point => point.Y)
            .ToArray();

        return new SpatialAnalyticsReport(Metrics(seats), ValidationTotals(validation), orderedMaps, orderedPoints, scenario);
    }

    private static IReadOnlyList<SeatEntry> ReadSeats(JsonObject maps, JsonObject assignments)
    {
        var byWorkstation = (assignments["assignments"]?.AsArray().OfType<JsonObject>() ?? [])
            .GroupBy(assignment => Text(assignment["workstationId"]), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        return (maps["maps"]?.AsArray().OfType<JsonObject>() ?? [])
            .SelectMany(map => (map["seats"]?.AsArray().OfType<JsonObject>() ?? []).Select(seat =>
            {
                var mapId = Text(map["id"]);
                var seatId = Text(seat["id"]);
                return new SeatEntry(
                    mapId,
                    TextOrNull(map["name"]),
                    seatId,
                    SeatStates.Derive(byWorkstation.GetValueOrDefault(seatId), seat),
                    NormalizedCoordinates(seat));
            }))
            .ToArray();
    }

    private static SpatialSeatMetrics Metrics(IEnumerable<SeatEntry> source)
    {
        var seats = source.ToArray();
        var total = seats.Length;
        var occupied = seats.Count(seat => seat.State == SeatState.Occupied);
        var free = seats.Count(seat => seat.State == SeatState.Free);
        var reserved = seats.Count(seat => seat.State == SeatState.Reserved);
        return new SpatialSeatMetrics(total, occupied, free, reserved, Percent(occupied, total), Percent(free, total));
    }

    private static SpatialValidationTotals ValidationTotals(IEnumerable<ValidationResult> results) => new(
        results.Count(result => result.Severity == ValidationSeverity.Critical),
        results.Count(result => result.Severity == ValidationSeverity.Warning),
        results.Count(result => result.Severity == ValidationSeverity.Info));

    private static void AddSeatStatePoints(IEnumerable<SeatEntry> seats, ICollection<SpatialHeatmapPoint> points)
    {
        foreach (var seat in seats.Where(seat => seat.Coordinates is not null))
        {
            var coordinates = seat.Coordinates!.Value;
            if (seat.State == SeatState.Occupied)
                points.Add(Point(seat, coordinates, OccupancyLayer, 1, seat.SeatId));
            else if (seat.State == SeatState.Free)
                points.Add(Point(seat, coordinates, AvailabilityLayer, 1, seat.SeatId));
        }
    }

    private static void AddProblemPoints(
        IReadOnlyList<SeatEntry> seats,
        IEnumerable<ValidationResult> validation,
        ICollection<SpatialHeatmapPoint> points)
    {
        foreach (var result in validation.OrderBy(result => result.Id, StringComparer.Ordinal))
        {
            var seenSeats = new HashSet<string>(StringComparer.Ordinal);
            foreach (var seat in SeatsForValidation(seats, result))
            {
                if (seat.Coordinates is not { } coordinates || !seenSeats.Add($"{seat.MapId}\u001f{seat.SeatId}")) continue;
                points.Add(Point(seat, coordinates, ProblemsLayer, SeverityValue(result.Severity), result.Id));
            }
        }
    }

    private static IEnumerable<SeatEntry> SeatsForValidation(IReadOnlyList<SeatEntry> seats, ValidationResult result)
    {
        var ids = result.RelatedEntityIds.Count > 0
            ? result.RelatedEntityIds
            : result.EntityType.Equals("workspace", StringComparison.OrdinalIgnoreCase) ? [result.EntityId] : [];
        return seats.Where(seat =>
            ids.Contains(seat.SeatId, StringComparer.Ordinal) &&
            (string.IsNullOrWhiteSpace(result.MapId) || string.Equals(seat.MapId, result.MapId, StringComparison.Ordinal)));
    }

    private static SpatialScenarioMetrics AddScenarioPoints(
        IReadOnlyList<SeatEntry> seats,
        IReadOnlyList<ScenarioDiffChange> changes,
        ICollection<SpatialHeatmapPoint> points)
    {
        var mappedChanges = 0;
        foreach (var change in changes.OrderBy(change => change.Id, StringComparer.Ordinal))
        {
            var fallbackSeat = SeatForChange(seats, change);
            var coordinates = NormalizedCoordinates(change.After) ?? NormalizedCoordinates(change.Before) ?? fallbackSeat?.Coordinates;
            if (coordinates is null) continue;

            var mapId = TextOrNull(change.MapId) ?? TextOrNull(change.After?["mapId"]) ?? TextOrNull(change.Before?["mapId"]) ?? fallbackSeat?.MapId;
            if (mapId is null) continue;
            var mapName = TextOrNull(change.MapName) ?? TextOrNull(change.After?["mapName"]) ?? TextOrNull(change.Before?["mapName"]) ?? fallbackSeat?.MapName;
            points.Add(new SpatialHeatmapPoint(mapId, mapName, change.EntityId, coordinates.Value.X, coordinates.Value.Y, ScenarioChangesLayer, 1, change.Id));
            mappedChanges++;
        }
        return new SpatialScenarioMetrics(changes.Count, mappedChanges);
    }

    private static SeatEntry? SeatForChange(IReadOnlyList<SeatEntry> seats, ScenarioDiffChange change)
    {
        var matches = seats.Where(seat => string.Equals(seat.SeatId, change.EntityId, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(change.MapId))
            matches = matches.Where(seat => string.Equals(seat.MapId, change.MapId, StringComparison.Ordinal));
        var candidates = matches.OrderBy(seat => seat.MapId, StringComparer.Ordinal).ThenBy(seat => seat.SeatId, StringComparer.Ordinal).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static SpatialHeatmapPoint Point(SeatEntry seat, Coordinates coordinates, string layer, int value, string sourceId) =>
        new(seat.MapId, seat.MapName, seat.SeatId, coordinates.X, coordinates.Y, layer, value, sourceId);

    private static Coordinates? NormalizedCoordinates(JsonObject? value) => NormalizedCoordinates(value?["x"], value?["y"]);

    private static Coordinates? NormalizedCoordinates(JsonNode? xNode, JsonNode? yNode)
    {
        if (!Coordinate(xNode, out var x) || !Coordinate(yNode, out var y)) return null;
        return new Coordinates(Convert.ToDecimal(x), Convert.ToDecimal(y));
    }

    private static bool Coordinate(JsonNode? node, out double value)
    {
        value = 0;
        return node is JsonValue json && json.TryGetValue<double>(out value) && double.IsFinite(value) && value is >= 0 and <= 1;
    }

    private static decimal Percent(int value, int total) => total == 0 ? 0 : decimal.Round(value * 100m / total, 2, MidpointRounding.AwayFromZero);

    private static int SeverityValue(ValidationSeverity severity) => severity switch
    {
        ValidationSeverity.Critical => 3,
        ValidationSeverity.Warning => 2,
        ValidationSeverity.Info => 1,
        _ => 0
    };

    private static int LayerOrder(string layer) => layer switch
    {
        OccupancyLayer => 0,
        AvailabilityLayer => 1,
        ProblemsLayer => 2,
        ScenarioChangesLayer => 3,
        _ => 4
    };

    private static string Text(JsonNode? value) => value?.ToString() ?? "";
    private static string? TextOrNull(JsonNode? value) => string.IsNullOrWhiteSpace(Text(value)) ? null : Text(value);
    private static string? TextOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record MapKey(string MapId, string? MapName);
    private sealed record SeatEntry(string MapId, string? MapName, string SeatId, SeatState State, Coordinates? Coordinates);
    private readonly record struct Coordinates(decimal X, decimal Y);
}
