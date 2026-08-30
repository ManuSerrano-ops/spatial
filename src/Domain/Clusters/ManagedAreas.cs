using System.IO;
using System.Text.Json.Nodes;

namespace PlanoOpenSpaceIT.Windows;

internal sealed record ManagedAreaMutation(JsonObject Document, bool Changed, IReadOnlyList<string> AreaIds, IReadOnlyList<string> WorkspaceIds);

internal static class ManagedAreas
{
    internal const string FileName = "managed-areas.json";

    internal static JsonObject EmptyDocument() => new()
    {
        ["schemaVersion"] = "1.0",
        ["version"] = 0,
        ["areas"] = new JsonArray()
    };

    internal static JsonObject Normalize(JsonObject? source, JsonObject maps)
    {
        var document = source is null ? EmptyDocument() : (JsonObject)source.DeepClone();
        var mapWorkspaces = MapWorkspaces(maps);
        var normalized = new List<JsonObject>();
        var areaIds = new HashSet<string>(StringComparer.Ordinal);
        var memberships = new HashSet<string>(StringComparer.Ordinal);

        var areas = document["areas"] as JsonArray ?? (source is null ? new JsonArray() : throw new InvalidDataException("managed-areas.json no contiene una colección de áreas válida."));
        foreach (var node in areas)
        {
            var area = node as JsonObject ?? throw new InvalidDataException("managed-areas.json contiene un área inválida.");
            var id = RequiredText(area["id"], "Cada Managed Area necesita un id.");
            var mapId = RequiredText(area["mapId"], $"La Managed Area {id} necesita un plano.");
            var name = RequiredText(area["name"], $"La Managed Area {id} necesita un nombre.");
            if (!areaIds.Add(id)) throw new InvalidDataException($"La Managed Area {id} está duplicada.");
            if (!mapWorkspaces.TryGetValue(mapId, out var workspaces)) throw new InvalidDataException($"El plano {mapId} de la Managed Area {id} no existe.");

            var workspaceIds = CanonicalIds(area["workspaceIds"], $"La Managed Area {id} no contiene una lista de puestos válida.");
            foreach (var workspaceId in workspaceIds)
            {
                if (!workspaces.Contains(workspaceId)) throw new InvalidDataException($"El puesto {workspaceId} no existe en el plano {mapId}.");
                if (!memberships.Add(MembershipKey(mapId, workspaceId))) throw new InvalidDataException($"El puesto {workspaceId} pertenece a más de una Managed Area en el plano {mapId}.");
            }

            normalized.Add(Area(id, mapId, name, workspaceIds));
        }

        normalized.Sort(CompareAreas);
        document["schemaVersion"] = Text(document["schemaVersion"]) is { Length: > 0 } schemaVersion ? schemaVersion : "1.0";
        if (document["version"] is null) document["version"] = 0;
        document["areas"] = new JsonArray(normalized.Select(area => (JsonNode?)area).ToArray());
        return document;
    }

    internal static ManagedAreaMutation Create(JsonObject source, JsonObject maps, string id, string mapId, string name, IEnumerable<string> workspaceIds)
    {
        var document = Normalize(source, maps);
        id = RequiredText(id, "La Managed Area necesita un id.");
        mapId = RequiredText(mapId, "La Managed Area necesita un plano.");
        name = RequiredText(name, "La Managed Area necesita un nombre.");
        var ids = CanonicalIds(workspaceIds);
        var areas = Areas(document);
        if (Find(areas, id) is not null) throw new InvalidDataException($"La Managed Area {id} ya existe.");
        if (areas.OfType<JsonObject>().Any(area => Text(area["mapId"]) == mapId && string.Equals(Text(area["name"]), name, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException($"Ya existe un cluster con el nombre {name} en este plano.");
        areas.Add(Area(id, mapId, name, ids));
        var result = Normalize(document, maps);
        return Changed(source, result, [id], ids);
    }

    internal static ManagedAreaMutation CreateWithMoves(JsonObject source, JsonObject maps, string id, string mapId, string name, IEnumerable<string> workspaceIds, IEnumerable<string> moveWorkspaceIds)
    {
        var document = Normalize(source, maps);
        id = RequiredText(id, "La Managed Area necesita un id.");
        mapId = RequiredText(mapId, "La Managed Area necesita un plano.");
        name = RequiredText(name, "La Managed Area necesita un nombre.");
        var ids = CanonicalIds(workspaceIds);
        var moving = CanonicalIds(moveWorkspaceIds);
        var idSet = ids.ToHashSet(StringComparer.Ordinal);
        if (moving.Any(id => !idSet.Contains(id))) throw new InvalidDataException("Solo se pueden mover puestos incluidos en el nuevo cluster.");
        var areas = Areas(document);
        if (Find(areas, id) is not null) throw new InvalidDataException($"La Managed Area {id} ya existe.");
        if (areas.OfType<JsonObject>().Any(area => Text(area["mapId"]) == mapId && string.Equals(Text(area["name"]), name, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException($"Ya existe un cluster con el nombre {name} en este plano.");
        var movingSet = moving.ToHashSet(StringComparer.Ordinal);
        foreach (var area in areas.OfType<JsonObject>().Where(area => Text(area["mapId"]) == mapId))
        {
            var members = CanonicalIds(area["workspaceIds"], "La lista de puestos de la Managed Area no es válida.");
            var ownedMoving = members.Where(movingSet.Contains).ToArray();
            if (ownedMoving.Length > 0) area["workspaceIds"] = JsonIds(members.Where(member => !movingSet.Contains(member)));
        }
        areas.Add(Area(id, mapId, name, ids));
        var result = Normalize(document, maps);
        return Changed(source, result, [id], ids);
    }

    internal static ManagedAreaMutation Rename(JsonObject source, JsonObject maps, string areaId, string name)
    {
        var document = Normalize(source, maps);
        name = RequiredText(name, "La Managed Area necesita un nombre.");
        var area = RequireArea(document, areaId);
        area["name"] = name;
        var result = Normalize(document, maps);
        return Changed(source, result, [Text(area["id"])], []);
    }

    internal static ManagedAreaMutation AddWorkspaces(JsonObject source, JsonObject maps, string areaId, IEnumerable<string> workspaceIds)
    {
        var document = Normalize(source, maps);
        var area = RequireArea(document, areaId);
        var additions = CanonicalIds(workspaceIds);
        var current = CanonicalIds(area["workspaceIds"], "La lista de puestos de la Managed Area no es válida.");
        area["workspaceIds"] = JsonIds(current.Concat(additions));
        var result = Normalize(document, maps);
        return Changed(source, result, [Text(area["id"])], additions);
    }

    internal static ManagedAreaMutation RemoveWorkspaces(JsonObject source, JsonObject maps, string areaId, IEnumerable<string> workspaceIds)
    {
        var document = Normalize(source, maps);
        var area = RequireArea(document, areaId);
        var removals = CanonicalIds(workspaceIds);
        var removeSet = removals.ToHashSet(StringComparer.Ordinal);
        var current = CanonicalIds(area["workspaceIds"], "La lista de puestos de la Managed Area no es válida.");
        area["workspaceIds"] = JsonIds(current.Where(id => !removeSet.Contains(id)));
        var result = Normalize(document, maps);
        return Changed(source, result, [Text(area["id"])], removals);
    }

    internal static ManagedAreaMutation MoveWorkspaces(JsonObject source, JsonObject maps, string fromAreaId, string toAreaId, IEnumerable<string> workspaceIds)
    {
        var document = Normalize(source, maps);
        var from = RequireArea(document, fromAreaId);
        var to = RequireArea(document, toAreaId);
        if (Text(from["id"]) == Text(to["id"])) throw new InvalidDataException("Las Managed Areas de origen y destino deben ser distintas.");
        if (Text(from["mapId"]) != Text(to["mapId"])) throw new InvalidDataException("No se pueden mover puestos entre Managed Areas de planos distintos.");

        var moving = CanonicalIds(workspaceIds);
        var fromIds = CanonicalIds(from["workspaceIds"], "La lista de puestos de la Managed Area de origen no es válida.");
        var fromSet = fromIds.ToHashSet(StringComparer.Ordinal);
        foreach (var workspaceId in moving)
            if (!fromSet.Contains(workspaceId)) throw new InvalidDataException($"El puesto {workspaceId} no pertenece a la Managed Area {Text(from["id"])}.");

        var movingSet = moving.ToHashSet(StringComparer.Ordinal);
        from["workspaceIds"] = JsonIds(fromIds.Where(id => !movingSet.Contains(id)));
        to["workspaceIds"] = JsonIds(CanonicalIds(to["workspaceIds"], "La lista de puestos de la Managed Area de destino no es válida.").Concat(moving));
        var result = Normalize(document, maps);
        return Changed(source, result, [Text(from["id"]), Text(to["id"])], moving);
    }

    internal static ManagedAreaMutation Merge(JsonObject source, JsonObject maps, string targetAreaId, IEnumerable<string> sourceAreaIds)
    {
        var document = Normalize(source, maps);
        var target = RequireArea(document, targetAreaId);
        var sourceIds = CanonicalIds(sourceAreaIds).Where(id => id != Text(target["id"])).ToArray();
        if (sourceIds.Length == 0) throw new InvalidDataException("Selecciona al menos una Managed Area de origen.");
        var sources = sourceIds.Select(id => RequireArea(document, id)).ToArray();
        if (sources.Any(area => Text(area["mapId"]) != Text(target["mapId"]))) throw new InvalidDataException("No se pueden fusionar Managed Areas de planos distintos.");

        var workspaceIds = CanonicalIds(target["workspaceIds"], "La lista de puestos de la Managed Area de destino no es válida.")
            .Concat(sources.SelectMany(area => CanonicalIds(area["workspaceIds"], "La lista de puestos de una Managed Area de origen no es válida.")));
        target["workspaceIds"] = JsonIds(workspaceIds);
        RemoveAreas(document, sourceIds);
        var result = Normalize(document, maps);
        return Changed(source, result, sourceIds.Prepend(Text(target["id"])), CanonicalIds(target["workspaceIds"], "La lista fusionada no es válida."));
    }

    internal static ManagedAreaMutation Dissolve(JsonObject source, JsonObject maps, string areaId)
    {
        var document = Normalize(source, maps);
        var area = RequireArea(document, areaId);
        var id = Text(area["id"]);
        var workspaceIds = CanonicalIds(area["workspaceIds"], "La lista de puestos de la Managed Area no es válida.");
        RemoveAreas(document, [id]);
        var result = Normalize(document, maps);
        return Changed(source, result, [id], workspaceIds);
    }

    internal static ManagedAreaMutation DeleteAndMove(JsonObject source, JsonObject maps, string sourceAreaId, string targetAreaId)
    {
        var document = Normalize(source, maps);
        var sourceArea = RequireArea(document, sourceAreaId);
        var targetArea = RequireArea(document, targetAreaId);
        if (Text(sourceArea["id"]) == Text(targetArea["id"])) throw new InvalidDataException("Las Managed Areas de origen y destino deben ser distintas.");
        if (Text(sourceArea["mapId"]) != Text(targetArea["mapId"])) throw new InvalidDataException("No se puede eliminar y mover entre Managed Areas de planos distintos.");

        var workspaceIds = CanonicalIds(sourceArea["workspaceIds"], "La lista de puestos de la Managed Area de origen no es válida.");
        targetArea["workspaceIds"] = JsonIds(CanonicalIds(targetArea["workspaceIds"], "La lista de puestos de la Managed Area de destino no es válida.").Concat(workspaceIds));
        RemoveAreas(document, [Text(sourceArea["id"])]);
        var result = Normalize(document, maps);
        return Changed(source, result, [Text(sourceArea["id"]), Text(targetArea["id"])], workspaceIds);
    }

    internal static bool ContainsWorkspace(JsonObject source, JsonObject maps, string mapId, string workspaceId) =>
        Normalize(source, maps)["areas"]!.AsArray().OfType<JsonObject>().Any(area => Text(area["mapId"]) == mapId && CanonicalIds(area["workspaceIds"], "La lista de puestos de la Managed Area no es válida.").Contains(workspaceId, StringComparer.Ordinal));

    private static ManagedAreaMutation Changed(JsonObject before, JsonObject after, IEnumerable<string> areaIds, IEnumerable<string> workspaceIds)
    {
        var canonicalBefore = (JsonArray)(before["areas"]?.DeepClone() ?? new JsonArray());
        var canonicalAfter = after["areas"]!.AsArray();
        return new ManagedAreaMutation(after, !JsonNode.DeepEquals(canonicalBefore, canonicalAfter), CanonicalIds(areaIds), CanonicalIds(workspaceIds));
    }

    private static Dictionary<string, HashSet<string>> MapWorkspaces(JsonObject maps)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var values = maps["maps"] as JsonArray ?? throw new InvalidDataException("maps.json no contiene una colección de planos válida.");
        foreach (var node in values)
        {
            var map = node as JsonObject ?? throw new InvalidDataException("maps.json contiene un plano inválido.");
            var mapId = RequiredText(map["id"], "Hay un plano sin id.");
            if (!result.TryAdd(mapId, new HashSet<string>(StringComparer.Ordinal))) throw new InvalidDataException($"El plano {mapId} está duplicado.");
            var seats = map["seats"] as JsonArray ?? new JsonArray();
            foreach (var seatNode in seats)
            {
                var seat = seatNode as JsonObject ?? throw new InvalidDataException($"El plano {mapId} contiene un puesto inválido.");
                var workspaceId = RequiredText(seat["id"], $"El plano {mapId} contiene un puesto sin id.");
                if (!result[mapId].Add(workspaceId)) throw new InvalidDataException($"El puesto {workspaceId} está duplicado en el plano {mapId}.");
            }
        }
        return result;
    }

    private static JsonArray Areas(JsonObject document) => document["areas"]?.AsArray() ?? throw new InvalidDataException("Falta la colección de Managed Areas.");
    private static JsonObject? Find(JsonArray areas, string id) => areas.OfType<JsonObject>().FirstOrDefault(area => Text(area["id"]) == id.Trim());
    private static JsonObject RequireArea(JsonObject document, string areaId) => Find(Areas(document), RequiredText(areaId, "Selecciona una Managed Area.")) ?? throw new InvalidDataException($"La Managed Area {areaId.Trim()} no existe.");
    private static JsonObject Area(string id, string mapId, string name, IEnumerable<string> workspaceIds) => new() { ["id"] = id, ["mapId"] = mapId, ["name"] = name, ["workspaceIds"] = JsonIds(workspaceIds) };
    private static JsonArray JsonIds(IEnumerable<string> values) => new(CanonicalIds(values).Select(id => (JsonNode?)id).ToArray());
    private static string[] CanonicalIds(JsonNode? node, string error) => node is JsonArray values ? CanonicalIds(values.Select(Text)) : throw new InvalidDataException(error);
    private static string[] CanonicalIds(IEnumerable<string> values) => values.Select(value => value?.Trim() ?? "").Where(value => value.Length > 0).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    private static string RequiredText(JsonNode? value, string error) => RequiredText(Text(value), error);
    private static string RequiredText(string? value, string error) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new InvalidDataException(error);
    private static string Text(JsonNode? value) => value?.ToString()?.Trim() ?? "";
    private static string MembershipKey(string mapId, string workspaceId) => mapId + "\0" + workspaceId;
    private static int CompareAreas(JsonObject left, JsonObject right) { var byMap = StringComparer.Ordinal.Compare(Text(left["mapId"]), Text(right["mapId"])); return byMap != 0 ? byMap : StringComparer.Ordinal.Compare(Text(left["id"]), Text(right["id"])); }
    private static void RemoveAreas(JsonObject document, IEnumerable<string> ids)
    {
        var remove = ids.ToHashSet(StringComparer.Ordinal);
        var areas = Areas(document);
        for (var index = areas.Count - 1; index >= 0; index--)
            if (areas[index] is JsonObject area && remove.Contains(Text(area["id"]))) areas.RemoveAt(index);
    }
}
