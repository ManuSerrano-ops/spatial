using System.IO;
using System.Text.Json.Nodes;

namespace PlanoOpenSpaceIT.Windows;

internal sealed class ManagedAreaService
{
    private readonly Storage _storage;

    internal ManagedAreaService(Storage storage)
    {
        _storage = storage;
    }

    internal JsonObject Create(JsonObject payload)
    {
        ValidatePayloadFields(payload, "createManagedArea", ["id", "areaId", "mapId", "name", "workspaceIds", "moveWorkspaceIds"]);
        var id = FirstText(payload, "id", "areaId");
        if (id.Length == 0) id = $"managed-area-{Guid.NewGuid():N}";
        var mapId = Required(payload, "mapId", "La Managed Area necesita un plano.");
        var name = Required(payload, "name", "La Managed Area necesita un nombre.");
        var workspaceIds = PayloadIds(payload, "workspaceIds", required: false);
        var moveWorkspaceIds = PayloadIds(payload, "moveWorkspaceIds", required: false);
        return Mutate(source => moveWorkspaceIds.Length == 0
            ? ManagedAreas.Create(source.Document, source.Maps, id, mapId, name, workspaceIds)
            : ManagedAreas.CreateWithMoves(source.Document, source.Maps, id, mapId, name, workspaceIds, moveWorkspaceIds), "Cluster creado", name);
    }

    internal JsonObject Rename(JsonObject payload)
    {
        ValidatePayloadFields(payload, "renameManagedArea", ["areaId", "id", "name"]);
        var areaId = RequiredFirst(payload, "Selecciona una Managed Area.", "areaId", "id");
        var name = Required(payload, "name", "La Managed Area necesita un nombre.");
        return Mutate(source => ManagedAreas.Rename(source.Document, source.Maps, areaId, name), "Managed Area renombrada", name);
    }

    internal JsonObject AddWorkspaces(JsonObject payload)
    {
        ValidatePayloadFields(payload, "addManagedAreaWorkspaces", ["areaId", "id", "workspaceIds"]);
        var areaId = RequiredFirst(payload, "Selecciona una Managed Area.", "areaId", "id");
        var workspaceIds = PayloadIds(payload, "workspaceIds", required: true);
        return Mutate(source => ManagedAreas.AddWorkspaces(source.Document, source.Maps, areaId, workspaceIds), "Puestos añadidos al cluster", $"{workspaceIds.Length} puestos");
    }

    internal JsonObject RemoveWorkspaces(JsonObject payload)
    {
        ValidatePayloadFields(payload, "removeManagedAreaWorkspaces", ["areaId", "id", "workspaceIds"]);
        var areaId = RequiredFirst(payload, "Selecciona una Managed Area.", "areaId", "id");
        var workspaceIds = PayloadIds(payload, "workspaceIds", required: true);
        return Mutate(source => ManagedAreas.RemoveWorkspaces(source.Document, source.Maps, areaId, workspaceIds), "Puestos retirados del cluster", $"{workspaceIds.Length} puestos");
    }

    internal JsonObject MoveWorkspaces(JsonObject payload)
    {
        ValidatePayloadFields(payload, "moveManagedAreaWorkspaces", ["fromAreaId", "sourceAreaId", "toAreaId", "targetAreaId", "workspaceIds"]);
        var fromAreaId = RequiredFirst(payload, "Selecciona la Managed Area de origen.", "fromAreaId", "sourceAreaId");
        var toAreaId = RequiredFirst(payload, "Selecciona la Managed Area de destino.", "toAreaId", "targetAreaId");
        var workspaceIds = PayloadIds(payload, "workspaceIds", required: true);
        return Mutate(source => ManagedAreas.MoveWorkspaces(source.Document, source.Maps, fromAreaId, toAreaId, workspaceIds), "Puestos movidos entre Managed Areas", $"{workspaceIds.Length} puestos");
    }

    internal JsonObject Merge(JsonObject payload)
    {
        ValidatePayloadFields(payload, "mergeManagedAreas", ["targetAreaId", "areaId", "sourceAreaIds"]);
        var targetAreaId = RequiredFirst(payload, "Selecciona la Managed Area de destino.", "targetAreaId", "areaId");
        var sourceAreaIds = PayloadIds(payload, "sourceAreaIds", required: true);
        return Mutate(source => ManagedAreas.Merge(source.Document, source.Maps, targetAreaId, sourceAreaIds), "Clusters fusionados", $"{sourceAreaIds.Length} clusters en {targetAreaId}");
    }

    internal JsonObject Dissolve(JsonObject payload)
    {
        ValidatePayloadFields(payload, "dissolveManagedArea", ["areaId", "id"]);
        var areaId = RequiredFirst(payload, "Selecciona una Managed Area.", "areaId", "id");
        var maps = _storage.ReadMaps();
        var current = ManagedAreas.Normalize(_storage.ReadManagedAreas(), maps);
        var area = current["areas"]!.AsArray().OfType<JsonObject>().FirstOrDefault(item => Text(item["id"]) == areaId) ?? throw new InvalidDataException($"La Managed Area {areaId} no existe.");
        var name = Text(area["name"]);
        var count = area["workspaceIds"]?.AsArray().Count ?? 0;
        return Mutate(source => ManagedAreas.Dissolve(source.Document, source.Maps, areaId), "Cluster disuelto", $"{name} · {count} puestos conservados");
    }

    internal JsonObject DeleteAndMove(JsonObject payload)
    {
        ValidatePayloadFields(payload, "deleteManagedAreaAndMove", ["areaId", "sourceAreaId", "targetAreaId", "toAreaId"]);
        var sourceAreaId = RequiredFirst(payload, "Selecciona la Managed Area que se eliminará.", "sourceAreaId", "areaId");
        var targetAreaId = RequiredFirst(payload, "Selecciona la Managed Area de destino.", "targetAreaId", "toAreaId");
        return Mutate(source => ManagedAreas.DeleteAndMove(source.Document, source.Maps, sourceAreaId, targetAreaId), "Managed Area eliminada y puestos movidos", $"{sourceAreaId} → {targetAreaId}");
    }

    internal JsonObject CreateSeatInArea(JsonObject state, AssignmentService.CreateSeatRequest request)
    {
        var managedAreas = ManagedAreas.Normalize(_storage.ReadManagedAreas(), state["maps"]!.AsObject());
        var targetArea = managedAreas["areas"]!.AsArray().OfType<JsonObject>().FirstOrDefault(area => Text(area["id"]) == request.TargetManagedAreaId)
            ?? throw new InvalidDataException("La zona gestionada ya no existe.");
        if (Text(targetArea["mapId"]) != request.MapId) throw new InvalidDataException("La zona gestionada pertenece a otro plano.");

        var targetName = Text(targetArea["name"]);
        var id = AssignmentService.AddSeat(state, request);
        var membership = ManagedAreas.AddWorkspaces(managedAreas, state["maps"]!.AsObject(), request.TargetManagedAreaId!, [id]);
        Bump(state["assignments"]!.AsObject());
        Bump(membership.Document);
        var documents = _storage.RealDocuments(state);
        documents[ManagedAreas.FileName] = membership.Document;
        _storage.Commit(documents, _storage.RealFiles.Append(ManagedAreas.FileName), "Antes de puesto creado en zona", "Puesto creado en zona", $"{id} creado en {targetName}", id);
        return new JsonObject { ["id"] = id, ["mapId"] = request.MapId, ["targetManagedAreaId"] = request.TargetManagedAreaId };
    }

    internal void EnsureWorkspaceIsNotManaged(JsonObject maps, string mapId, string workspaceId)
    {
        var document = _storage.ReadManagedAreas();
        if (document is not null && ManagedAreas.ContainsWorkspace(document, maps, mapId, workspaceId))
            throw new InvalidOperationException($"El puesto {workspaceId} pertenece a una Managed Area. Retíralo o disuelve el área antes de eliminarlo.");
    }

    internal void ValidateAgainstMaps(JsonObject maps)
    {
        var document = _storage.ReadManagedAreas();
        if (document is not null) ManagedAreas.Normalize(document, maps);
    }

    private JsonObject Mutate(Func<(JsonObject Document, JsonObject Maps), ManagedAreaMutation> mutation, string eventTitle, string eventDescription)
    {
        var maps = _storage.ReadMaps();
        var current = ManagedAreas.Normalize(_storage.ReadManagedAreas(), maps);
        var result = mutation((current, maps));
        if (!result.Changed) return Result(result, noOp: true);
        Bump(result.Document);
        _storage.Commit(
            new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase) { [ManagedAreas.FileName] = result.Document },
            [ManagedAreas.FileName],
            "Antes de " + eventTitle,
            eventTitle,
            eventDescription,
            null);
        return Result(result, noOp: false);
    }

    private static JsonObject Result(ManagedAreaMutation mutation, bool noOp) => new()
    {
        ["ok"] = true,
        ["noOp"] = noOp,
        ["areaIds"] = new JsonArray(mutation.AreaIds.Select(id => (JsonNode?)id).ToArray()),
        ["workspaceIds"] = new JsonArray(mutation.WorkspaceIds.Select(id => (JsonNode?)id).ToArray()),
        ["managedAreas"] = mutation.Document.DeepClone()
    };

    private static void Bump(JsonObject value)
    {
        value["version"] = (value["version"]?.GetValue<int>() ?? 0) + 1;
        value["updatedAt"] = DateTimeOffset.UtcNow.ToString("O");
        value["updatedBy"] = Environment.UserName;
    }

    private static string[] PayloadIds(JsonObject payload, string key, bool required)
    {
        if (payload[key] is null)
        {
            if (required) throw new InvalidDataException($"Falta la lista {key}.");
            return [];
        }
        if (payload[key] is not JsonArray values) throw new InvalidDataException($"{key} debe ser una lista.");
        return values.Select(Text).Select(value => value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static void ValidatePayloadFields(JsonObject payload, string action, IEnumerable<string> allowed)
    {
        var allowedFields = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, _) in payload)
            if (!allowedFields.Contains(name)) throw new InvalidDataException($"Campo no reconocido: '{name}' en {action}");
    }

    private static string FirstText(JsonObject payload, params string[] keys) => keys.Select(key => Text(payload[key]).Trim()).FirstOrDefault(value => value.Length > 0) ?? "";
    private static string RequiredFirst(JsonObject payload, string error, params string[] keys) => FirstText(payload, keys) is { Length: > 0 } value ? value : throw new InvalidDataException(error);
    private static string Required(JsonObject payload, string key, string error) => Text(payload[key]) is { Length: > 0 } value ? value : throw new InvalidDataException(error);
    private static string Text(JsonNode? value) => value?.ToString() ?? "";

    internal sealed record Storage(
        Func<JsonObject> ReadMaps,
        Func<JsonObject?> ReadManagedAreas,
        Func<JsonObject, Dictionary<string, JsonObject>> RealDocuments,
        IEnumerable<string> RealFiles,
        Action<Dictionary<string, JsonObject>, IEnumerable<string>, string, string, string, string?> Commit);
}
