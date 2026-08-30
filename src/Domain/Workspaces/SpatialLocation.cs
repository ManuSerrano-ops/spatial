using System.Text.Json.Nodes;

namespace PlanoOpenSpaceIT.Windows;

internal static class SpatialLocation
{
    internal const int Columns = 24;
    internal const int Rows = 18;

    internal static string FromSeat(JsonObject seat)
    {
        ArgumentNullException.ThrowIfNull(seat);
        return FromCoordinates(seat["x"], seat["y"]) ?? Text(seat["gridCell"]);
    }

    internal static string? FromCoordinates(JsonNode? xNode, JsonNode? yNode)
    {
        if (!Coordinate(xNode, out var x) || !Coordinate(yNode, out var y) || x is < 0 or > 1 || y is < 0 or > 1) return null;
        var column = Math.Clamp((int)Math.Floor(x * Columns), 0, Columns - 1);
        var row = Math.Clamp((int)Math.Floor(y * Rows), 0, Rows - 1);
        return $"{ColumnName(column)}-{row + 1:D2}";
    }

    private static bool Coordinate(JsonNode? node, out double value)
    {
        value = 0;
        return node is JsonValue json && json.TryGetValue<double>(out value);
    }

    private static string ColumnName(int column) { var value = ""; for (column++; column > 0; column = (column - 1) / 26) value = (char)('A' + (column - 1) % 26) + value; return value; }
    private static string Text(JsonNode? node) => node?.ToString() ?? "";
}
