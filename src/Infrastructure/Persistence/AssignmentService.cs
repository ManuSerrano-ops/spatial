using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlanoOpenSpaceIT.Windows;

internal sealed class AssignmentService
{
    private const int GridColumns = 24;
    private const int GridRows = 18;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly Storage _storage;

    internal AssignmentService(Storage storage)
    {
        _storage = storage;
    }

    internal JsonObject SaveAssignment(JsonObject payload, bool delete)
    {
        if (delete)
        {
            var request = DeleteAssignmentRequest.From(payload);
            if (request.ScenarioId is not null) return _storage.MutateScenario(request.ScenarioId, draft => DeleteAssignment(draft, request));
            var state = _storage.RealState();
            DeleteAssignment(state, request);
            _storage.CommitReal(state, "Asignación eliminada", request.WorkstationId!);
            return new JsonObject { ["ok"] = true };
        }

        var save = SaveAssignmentRequest.From(payload);
        if (save.ScenarioId is not null)
        {
            var draft = _storage.FindScenario(save.ScenarioId)["draft"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
            var warnings = ValidateAssignment(draft, save);
            var result = _storage.MutateScenario(save.ScenarioId, state => SetAssignment(state, save));
            result["warnings"] = WarningArray(warnings);
            return result;
        }
        var real = _storage.RealState();
        var realWarnings = ValidateAssignment(real, save);
        SetAssignment(real, save);
        _storage.CommitReal(real, "Asignación guardada", save.WorkstationId!);
        return new JsonObject { ["ok"] = true, ["warnings"] = WarningArray(realWarnings) };
    }

    internal JsonObject BulkUpdateAssignments(JsonObject payload)
    {
        var request = BulkAssignmentRequest.From(payload);
        if (request.WorkstationIds!.Count == 0) return BulkAssignmentResult(request, 0);

        int Apply(JsonObject state)
        {
            var seats = state["maps"]?["maps"]?.AsArray().OfType<JsonObject>().SelectMany(map => map["seats"]?.AsArray().OfType<JsonObject>() ?? []).ToDictionary(seat => Text(seat["id"]), StringComparer.Ordinal) ?? [];
            var assignments = state["assignments"]?["assignments"]?.AsArray().OfType<JsonObject>().Where(item => Text(item["workstationId"]).Length > 0).GroupBy(item => Text(item["workstationId"]), StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal) ?? [];
            var updates = new List<string>();
            foreach (var workstationId in request.WorkstationIds)
            {
                if (!seats.TryGetValue(workstationId, out var seat)) throw new InvalidDataException($"El puesto {workstationId} ya no existe.");
                var effectiveState = SeatStates.DeriveEffectiveWorkspaceState(seat, assignments.GetValueOrDefault(workstationId)).State;
                if (request.Status == "reserved")
                {
                    if (effectiveState == SeatState.Occupied) throw new InvalidDataException($"El puesto {workstationId} está ocupado y no se puede reservar.");
                    if (effectiveState == SeatState.Free) updates.Add(workstationId);
                }
                else if (effectiveState == SeatState.Reserved)
                {
                    updates.Add(workstationId);
                }
            }
            foreach (var workstationId in updates)
            {
                var update = new SaveAssignmentRequest(workstationId, null, null, null, null, request.Status, null, null, request.ScenarioId, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "status" });
                SetAssignment(state, update);
            }
            return updates.Count;
        }

        if (request.ScenarioId is not null)
        {
            var scenario = _storage.FindScenario(request.ScenarioId);
            var draft = scenario["draft"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
            var preview = (JsonObject)draft.DeepClone();
            var updated = Apply(preview);
            if (updated == 0) return BulkAssignmentResult(request, 0);
            _storage.MutateScenario(request.ScenarioId, state => Apply(state));
            return BulkAssignmentResult(request, updated);
        }

        var real = _storage.RealState();
        var realUpdated = Apply(real);
        if (realUpdated == 0) return BulkAssignmentResult(request, 0);
        var title = request.Status == "reserved" ? "Puestos reservados" : "Reservas retiradas";
        _storage.CommitReal(real, title, $"{realUpdated} puestos");
        return BulkAssignmentResult(request, realUpdated);
    }

    internal JsonObject SavePosition(JsonObject payload)
    {
        var request = SavePositionRequest.From(payload);
        if (request.ScenarioId is not null) return _storage.MutateScenario(request.ScenarioId, draft => SetPosition(draft, request));
        var state = _storage.RealState();
        SetPosition(state, request);
        _storage.CommitReal(state, "Puesto movido", request.SeatId!);
        return new JsonObject { ["ok"] = true };
    }

    internal JsonObject CreateSeat(JsonObject payload, Func<JsonObject, CreateSeatRequest, JsonObject>? createInManagedArea = null)
    {
        var request = CreateSeatRequest.From(payload);
        if (request.ScenarioId is not null)
        {
            if (request.TargetManagedAreaId is not null) throw new InvalidOperationException("No se puede crear un puesto dentro de una zona gestionada desde un escenario: la pertenencia debe confirmarse en la realidad.");
            string id = "";
            _storage.MutateScenario(request.ScenarioId, draft => id = AddSeat(draft, request));
            return new JsonObject { ["id"] = id, ["mapId"] = request.MapId };
        }

        var state = _storage.RealState();
        if (request.TargetManagedAreaId is not null)
        {
            if (createInManagedArea is null) throw new InvalidOperationException("La creación en zona gestionada requiere coordinación de área.");
            return createInManagedArea(state, request);
        }

        var created = AddSeat(state, request);
        _storage.CommitReal(state, "Puesto creado", created);
        return new JsonObject { ["id"] = created, ["mapId"] = request.MapId };
    }

    internal JsonObject DeleteSeat(JsonObject payload, Action<JsonObject, string, string> ensureWorkspaceIsNotManaged)
    {
        var request = DeleteSeatRequest.From(payload);
        if (request.ScenarioId is not null) return _storage.MutateScenario(request.ScenarioId, draft => RemoveSeat(draft, request));
        var state = _storage.RealState();
        ensureWorkspaceIsNotManaged(state["maps"]!.AsObject(), request.MapId!, request.SeatId!);
        RemoveSeat(state, request);
        _storage.CommitReal(state, "Puesto eliminado", request.SeatId!);
        return new JsonObject { ["ok"] = true };
    }

    internal static string AddSeat(JsonObject state, CreateSeatRequest request)
    {
        var map = Map(state, request.MapId!);
        var seats = map["seats"]?.AsArray() ?? new JsonArray();
        var x = Coordinate(request.X);
        var y = Coordinate(request.Y);
        var id = "custom-" + Guid.NewGuid().ToString("N");
        var cell = Cell(x, y);
        var name = $"{MapPrefix(Text(map["id"]))}-{cell}";
        seats.Add(new JsonObject { ["id"] = id, ["name"] = name, ["type"] = "free", ["x"] = x, ["y"] = y, ["gridCell"] = cell, ["updatedBy"] = Environment.UserName });
        map["seats"] = seats;
        return id;
    }

    internal static string Cell(double x, double y)
    {
        var column = Math.Clamp((int)Math.Floor(x * GridColumns), 0, GridColumns - 1);
        var row = Math.Clamp((int)Math.Floor(y * GridRows), 0, GridRows - 1);
        return $"{ColumnName(column)}-{row + 1:D2}";
    }

    private static JsonObject BulkAssignmentResult(BulkAssignmentRequest request, int updated) => new()
    {
        ["ok"] = true,
        ["updated"] = updated,
        ["requested"] = request.WorkstationIds?.Count ?? 0,
        ["status"] = request.Status,
        ["action"] = request.Status == "reserved" ? "reserve" : "removeReservation",
        ["noOp"] = updated == 0
    };

    private static string[] ValidateAssignment(JsonObject state, SaveAssignmentRequest request)
    {
        var values = state["assignments"]?["assignments"]?.AsArray().OfType<JsonObject>() ?? [];
        var duplicateRoseta = request.Has("roseta") && !string.IsNullOrWhiteSpace(request.Roseta)
            ? values.FirstOrDefault(item => Text(item["workstationId"]) != request.WorkstationId && string.Equals(Text(item["roseta"]).Trim(), request.Roseta.Trim(), StringComparison.OrdinalIgnoreCase))
            : null;
        if (duplicateRoseta is not null) throw new InvalidDataException(DuplicateRosetaMessage(state, duplicateRoseta));
        if (request.Has("deviceId") && !string.IsNullOrWhiteSpace(request.DeviceId) && values.Any(item => Text(item["workstationId"]) != request.WorkstationId && string.Equals(Text(item["deviceId"]), request.DeviceId, StringComparison.Ordinal))) throw new InvalidDataException("El dispositivo ya está asignado a otro puesto.");
        var warnings = new List<string>();
        if (request.Has("personId") && !string.IsNullOrWhiteSpace(request.PersonId) && values.Any(item => Text(item["workstationId"]) != request.WorkstationId && string.Equals(Text(item["personId"]), request.PersonId, StringComparison.Ordinal))) warnings.Add("La persona ya tiene otra asignación; se ha guardado igualmente.");
        return warnings.ToArray();
    }

    private static string DuplicateRosetaMessage(JsonObject state, JsonObject assignment)
    {
        var workstationId = Text(assignment["workstationId"]);
        var seat = Seats(state).Values.FirstOrDefault(item => Text(item["id"]) == workstationId);
        var seatName = Text(seat?["name"]);
        if (seatName.Length == 0) seatName = workstationId.Length == 0 ? "No disponible" : workstationId;
        var mapName = Text(seat?["mapName"]);
        var cell = GridCell(seat);
        var position = string.Join(", ", new[] { mapName, cell }.Where(value => value.Length > 0));
        if (position.Length == 0) position = "No disponible";
        return $"La roseta «{Text(assignment["roseta"]).Trim()}» ya está asignada. Puesto: {seatName}. Posición: {position}. Persona: {ValueOrUnassigned(assignment["personId"])}. Equipo: {ValueOrUnassigned(assignment["deviceId"])}. Ubicación: {ValueOrUnassigned(assignment["locationId"])}.";
    }

    private static void SetAssignment(JsonObject state, SaveAssignmentRequest request)
    {
        var list = state["assignments"]?["assignments"]?.AsArray() ?? new JsonArray();
        var item = list.OfType<JsonObject>().FirstOrDefault(value => Text(value["workstationId"]) == request.WorkstationId!) is { } existing ? (JsonObject)existing.DeepClone() : new JsonObject();
        Remove(list, value => Text(value?["workstationId"]) == request.WorkstationId!);
        item["workstationId"] = request.WorkstationId!;
        SetReceived(item, "personId", request.PersonId, request.Has("personId")); SetReceived(item, "deviceId", request.DeviceId, request.Has("deviceId")); SetReceived(item, "locationId", request.LocationId, request.Has("locationId")); SetReceived(item, "roseta", request.Roseta, request.Has("roseta")); SetReceived(item, "status", request.Status, request.Has("status")); SetReceived(item, "notes", request.Notes, request.Has("notes"));
        item.Remove("scenarioId"); item.Remove("seatName"); item["updatedAt"] = DateTimeOffset.UtcNow.ToString("O"); item["updatedBy"] = Environment.UserName;
        list.Add(item); UpdateSeatName(state, request.WorkstationId!, request.SeatName, request.Has("seatName"));
        state["assignments"] ??= New("assignments"); state["assignments"]!["assignments"] = list;
    }

    private static void DeleteAssignment(JsonObject state, DeleteAssignmentRequest request)
    {
        var list = state["assignments"]?["assignments"]?.AsArray() ?? new JsonArray();
        Remove(list, value => Text(value?["workstationId"]) == request.WorkstationId!); UpdateSeatName(state, request.WorkstationId!, request.SeatName, request.Has("seatName"));
        state["assignments"] ??= New("assignments"); state["assignments"]!["assignments"] = list;
    }

    private static void SetPosition(JsonObject state, SavePositionRequest request)
    {
        var seat = Seat(state, request.MapId!, request.SeatId!);
        var x = Coordinate(request.X);
        var y = Coordinate(request.Y);
        seat["x"] = x; seat["y"] = y; seat["gridCell"] = Cell(x, y); seat["updatedAt"] = DateTimeOffset.UtcNow.ToString("O"); seat["updatedBy"] = Environment.UserName;
    }

    private static void RemoveSeat(JsonObject state, DeleteSeatRequest request)
    {
        var map = Map(state, request.MapId!);
        Remove(map["seats"]?.AsArray() ?? new JsonArray(), seat => Text(seat?["id"]) == request.SeatId!);
        Remove(state["assignments"]?["assignments"]?.AsArray() ?? new JsonArray(), assignment => Text(assignment?["workstationId"]) == request.SeatId!);
    }

    private static void SetReceived(JsonObject item, string name, string? value, bool received) { if (received) item[name] = value; }
    private static void UpdateSeatName(JsonObject state, string workstationId, string? seatName, bool received)
    {
        if (!received) return;
        var editableSeat = state["maps"]?["maps"]?.AsArray().OfType<JsonObject>().SelectMany(map => map["seats"]?.AsArray().OfType<JsonObject>() ?? []).FirstOrDefault(item => Text(item["id"]) == workstationId);
        if (editableSeat is not null) editableSeat["name"] = seatName ?? "";
    }

    private static Dictionary<string, JsonObject> Seats(JsonObject state) => state["maps"]?["maps"]?.AsArray().OfType<JsonObject>().SelectMany(map => map["seats"]?.AsArray().OfType<JsonObject>().Select(seat => { var copy = (JsonObject)seat.DeepClone(); copy["mapId"] = map["id"]?.DeepClone(); copy["mapName"] = map["name"]?.DeepClone(); return copy; }) ?? []).ToDictionary(seat => Text(seat["mapId"]) + "|" + Text(seat["id"])) ?? [];
    private static JsonObject Map(JsonObject state, string id) => state["maps"]?["maps"]?.AsArray().OfType<JsonObject>().FirstOrDefault(map => Text(map["id"]) == id) ?? throw new InvalidDataException("Plano inexistente.");
    private static JsonObject Seat(JsonObject state, string mapId, string id) => Map(state, mapId)["seats"]?.AsArray().OfType<JsonObject>().FirstOrDefault(seat => Text(seat["id"]) == id) ?? throw new InvalidDataException("Puesto inexistente.");
    private static string GridCell(JsonNode? item) => item is JsonObject value && Coordinate(value["x"], out var x) && Coordinate(value["y"], out var y) ? Cell(x, y) : "";
    private static string ValueOrUnassigned(JsonNode? value) => Text(value) is { Length: > 0 } text ? text : "Sin asignar";
    private static JsonArray WarningArray(IEnumerable<string> warnings) => new(warnings.Select(value => JsonValue.Create(value)).ToArray());
    private static JsonObject New(string array) => new() { ["schemaVersion"] = "1.0", ["version"] = 0, [array] = new JsonArray() };
    private static void Remove(JsonArray list, Func<JsonNode?, bool> predicate) { for (var index = list.Count - 1; index >= 0; index--) if (predicate(list[index])) list.RemoveAt(index); }
    private static string MapPrefix(string mapId) => mapId.ToLowerInvariant() switch { "norte" => "NOR", "nivel3" => "N3", "sur" => "SUR", "id" => "ID", "qc" => "QC", _ => mapId.ToUpperInvariant() };
    private static string ColumnName(int column) { var result = ""; for (column++; column > 0; column = (column - 1) / 26) result = (char)('A' + (column - 1) % 26) + result; return result; }
    private static bool Coordinate(JsonNode? node, out double value) { value = 0; return node is JsonValue json && json.TryGetValue<double>(out value); }
    private static double Coordinate(double? value) => value is >= 0 and <= 1 ? value.Value : throw new InvalidDataException("Coordenada inválida.");
    private static string Text(JsonNode? value) => value?.ToString() ?? "";
    private static string? NormalizeScenarioId(string? scenarioId) => string.IsNullOrWhiteSpace(scenarioId) ? null : scenarioId;
    private static string Required(string? value, string error) => !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidDataException(error);

    internal sealed record Storage(
        Func<JsonObject> RealState,
        Func<string, JsonObject> FindScenario,
        Func<string, Action<JsonObject>, JsonObject> MutateScenario,
        Action<JsonObject, string, string> CommitReal);

    internal sealed record CreateSeatRequest(string? MapId, double? X, double? Y, string? ScenarioId, string? TargetManagedAreaId)
    {
        internal static CreateSeatRequest From(JsonObject payload)
        {
            var request = Bind<CreateSeatRequest>(payload, "createSeat", ["mapId", "x", "y", "scenarioId", "targetManagedAreaId"]);
            return request with { MapId = Required(request.MapId, "Plano inválido."), ScenarioId = NormalizeScenarioId(request.ScenarioId), TargetManagedAreaId = string.IsNullOrWhiteSpace(request.TargetManagedAreaId) ? null : request.TargetManagedAreaId.Trim() };
        }
    }

    private sealed record SaveAssignmentRequest(string? WorkstationId, string? PersonId, string? DeviceId, string? LocationId, string? Roseta, string? Status, string? Notes, string? SeatName, string? ScenarioId, IReadOnlySet<string>? ReceivedFields = null)
    {
        internal bool Has(string field) => ReceivedFields?.Contains(field) == true;
        internal static SaveAssignmentRequest From(JsonObject payload)
        {
            var (request, fields) = BindWithFields<SaveAssignmentRequest>(payload, "saveAssignment", ["workstationId", "personId", "deviceId", "locationId", "roseta", "status", "notes", "seatName", "scenarioId"]);
            return request with { WorkstationId = Required(request.WorkstationId, "Puesto inválido."), ScenarioId = NormalizeScenarioId(request.ScenarioId), ReceivedFields = fields };
        }
    }

    private sealed record DeleteAssignmentRequest(string? WorkstationId, string? SeatName, string? ScenarioId, IReadOnlySet<string>? ReceivedFields = null)
    {
        internal bool Has(string field) => ReceivedFields?.Contains(field) == true;
        internal static DeleteAssignmentRequest From(JsonObject payload)
        {
            var (request, fields) = BindWithFields<DeleteAssignmentRequest>(payload, "deleteAssignment", ["workstationId", "seatName", "scenarioId"]);
            return request with { WorkstationId = Required(request.WorkstationId, "Puesto inválido."), ScenarioId = NormalizeScenarioId(request.ScenarioId), ReceivedFields = fields };
        }
    }

    private sealed record BulkAssignmentRequest(List<string>? WorkstationIds, string? Status, string? ScenarioId)
    {
        internal static BulkAssignmentRequest From(JsonObject payload)
        {
            var request = Bind<BulkAssignmentRequest>(payload, "bulkUpdateAssignments", ["workstationIds", "status", "scenarioId"]);
            var ids = request.WorkstationIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? [];
            var status = request.Status?.Trim().ToLowerInvariant();
            if (status is not ("reserved" or "confirmed")) throw new InvalidDataException("El estado masivo debe ser reservado o automático.");
            return request with { WorkstationIds = ids, Status = status, ScenarioId = NormalizeScenarioId(request.ScenarioId) };
        }
    }

    private sealed record SavePositionRequest(string? MapId, string? SeatId, double? X, double? Y, string? ScenarioId)
    {
        internal static SavePositionRequest From(JsonObject payload)
        {
            var request = Bind<SavePositionRequest>(payload, "savePosition", ["mapId", "seatId", "x", "y", "scenarioId"]);
            return request with { MapId = Required(request.MapId, "Plano inválido."), SeatId = Required(request.SeatId, "Puesto inválido."), ScenarioId = NormalizeScenarioId(request.ScenarioId) };
        }
    }

    private sealed record DeleteSeatRequest(string? MapId, string? SeatId, string? ScenarioId)
    {
        internal static DeleteSeatRequest From(JsonObject payload)
        {
            var request = Bind<DeleteSeatRequest>(payload, "deleteSeat", ["mapId", "seatId", "scenarioId"]);
            return request with { MapId = Required(request.MapId, "Plano inválido."), SeatId = Required(request.SeatId, "Puesto inválido."), ScenarioId = NormalizeScenarioId(request.ScenarioId) };
        }
    }

    private static T Bind<T>(JsonObject payload, string action, IEnumerable<string> allowed)
    {
        ValidatePayloadFields(payload, action, allowed);
        return JsonSerializer.Deserialize<T>(payload.ToJsonString(), JsonOptions) ?? throw new InvalidDataException($"Payload inválido en {action}.");
    }

    private static (T Request, HashSet<string> Fields) BindWithFields<T>(JsonObject payload, string action, IEnumerable<string> allowed)
    {
        var allowedFields = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, _) in payload)
        {
            if (!allowedFields.Contains(name)) throw new InvalidDataException($"Campo no reconocido: '{name}' en {action}");
            fields.Add(name);
        }
        var request = JsonSerializer.Deserialize<T>(payload.ToJsonString(), JsonOptions) ?? throw new InvalidDataException($"Payload inválido en {action}.");
        return (request, fields);
    }

    private static void ValidatePayloadFields(JsonObject payload, string action, IEnumerable<string> allowed)
    {
        var allowedFields = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, _) in payload)
            if (!allowedFields.Contains(name)) throw new InvalidDataException($"Campo no reconocido: '{name}' en {action}");
    }
}
