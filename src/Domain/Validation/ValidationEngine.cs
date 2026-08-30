using System.Text.Json.Nodes;

namespace PlanoOpenSpaceIT.Windows;

internal enum ValidationSeverity { Critical, Warning, Info }
internal enum ValidationClassification { Operational, Historical }

internal sealed record ValidationResult(
    string Id, string RuleId, ValidationSeverity Severity, string EntityType,
    string EntityId, string? MapId, string? Field, string Title, string Message,
    string? Details, IReadOnlyList<string> RelatedEntityIds, string SuggestedAction,
    ValidationClassification Classification = ValidationClassification.Operational)
{
    internal bool IsOperational => Classification == ValidationClassification.Operational;
}

internal static class ValidationEngine
{
    internal static IReadOnlyList<ValidationResult> Run(JsonObject maps, JsonObject assignments, JsonObject? positions = null)
    {
        var seats = maps["maps"]?.AsArray().OfType<JsonObject>().SelectMany(map => map["seats"]?.AsArray().OfType<JsonObject>().Select(seat => new { MapId = Text(map["id"]), Id = Text(seat["id"]), Type = Text(seat["type"]), Seat = seat }) ?? []).ToArray() ?? [];
        var seatIds = seats.Select(seat => seat.Id).ToHashSet(StringComparer.Ordinal);
        var values = assignments["assignments"]?.AsArray().OfType<JsonObject>().ToArray() ?? [];
        var results = new List<ValidationResult>();
        AddDuplicates("duplicate-network-outlet", "roseta", ValidationSeverity.Critical, "Roseta duplicada", "Corregir una asignación de red.");
        AddDuplicates("duplicate-device", "deviceId", ValidationSeverity.Critical, "Equipo duplicado", "Revisar la asignación del equipo.");
        AddDuplicates("duplicate-person", "personId", ValidationSeverity.Warning, "Persona duplicada", "Revisar la asignación de la persona.");
        foreach (var assignment in values.Where(item => !seatIds.Contains(Text(item["workstationId"])))) Add("assignment-missing-workspace", ValidationSeverity.Critical, "assignment", Text(assignment["workstationId"]), null, "workstationId", "Asignación sin puesto", "La asignación referencia un puesto inexistente.", null, [], "Revisar referencia.");
        foreach (var seat in seats.Where(seat => seat.Type.Equals("occupied", StringComparison.OrdinalIgnoreCase) && !values.Any(a => Text(a["workstationId"]) == seat.Id))) Add("historical-occupied-without-assignment", ValidationSeverity.Info, "workspace", seat.Id, seat.MapId, "type", "Marca histórica sin asignación", "El dibujo heredado indica ocupado sin asignación vigente.", null, [], "Revisar dibujo heredado.", ValidationClassification.Historical);
        foreach (var seat in seats.Where(seat => !Number(seat.Seat["x"]) || !Number(seat.Seat["y"]))) Add("invalid-coordinate", ValidationSeverity.Critical, "workspace", seat.Id, seat.MapId, "x/y", "Coordenada inválida", "La coordenada no está normalizada entre 0 y 1.", null, [], "Corregir coordenada.");
        return results.OrderBy(result => result.Severity).ThenBy(result => result.RuleId).ThenBy(result => result.MapId).ThenBy(result => result.EntityId).ToArray();
        void AddDuplicates(string rule, string field, ValidationSeverity severity, string title, string action) { foreach (var group in values.Select(value => new { Value = Text(value[field]).Trim(), Seat = Text(value["workstationId"]) }).Where(value => value.Value.Length > 0).GroupBy(value => value.Value, StringComparer.OrdinalIgnoreCase).Where(group => group.Select(value => value.Seat).Distinct().Count() > 1)) Add(rule, severity, "assignment", group.Key, seats.FirstOrDefault(seat => seat.Id == group.First().Seat)?.MapId, field, title, $"{field} asignado a varios puestos.", $"Valor duplicado: {group.Key}.", group.Select(value => value.Seat).Order().ToArray(), action); }
        void Add(string rule, ValidationSeverity severity, string type, string id, string? map, string? field, string title, string message, string? details, IReadOnlyList<string> related, string action, ValidationClassification classification = ValidationClassification.Operational) => results.Add(new ValidationResult($"{rule}|{id}|{string.Join(',', related)}", rule, severity, type, id, map, field, title, message, details, related, action, classification));
    }

    internal static IReadOnlyList<ValidationResult> OperationalResults(IEnumerable<ValidationResult> results) => results.Where(result => result.IsOperational).ToArray();

    private static bool Number(JsonNode? value) => value is JsonValue json && json.TryGetValue<double>(out var number) && number is >= 0 and <= 1;
    private static string Text(JsonNode? value) => value?.ToString() ?? "";
}
