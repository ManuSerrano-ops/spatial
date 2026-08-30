using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PlanoOpenSpaceIT.Windows;

internal sealed class AppConfig
{
    public required string NetworkRoot { get; init; }
    public string DataFolder { get; init; } = "data";
    public string BackupFolder { get; init; } = "backups";
    public string LogsFolder { get; init; } = "logs";
    public long LogMaxFileSizeBytes { get; init; } = SafeLogger.DefaultMaxFileSizeBytes;
    public int LogMaxHistoryFiles { get; init; } = SafeLogger.DefaultMaxHistoryFiles;
    public string BackupRetentionMode { get; init; } = "disabled";
    public bool ReadOnly { get; init; }
}

internal sealed class DataStore
{
    private const int GridColumns = 24;
    private const int GridRows = 18;
    private const string LegacyScenarioApplyError = "Este escenario se creó antes del control de revisiones y no se puede aplicar con seguridad. Crea un escenario nuevo sobre la realidad actual.";
    private static readonly string[] RealFiles = ["maps.json", "assignments.json", "positions.json", "events.json"];
    private static readonly string[] OperationalBackupFiles = ["maps.json", "assignments.json", "positions.json", "events.json", "scenarios.json", "people.json", "devices.json", "locations.json", ManagedAreas.FileName];
    private static readonly string[] UserRestoreFiles = ["maps.json", "assignments.json", "positions.json", ManagedAreas.FileName];
    private static readonly HashSet<string> TransactionFiles = new(RealFiles.Append("scenarios.json").Append(ManagedAreas.FileName), StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private static readonly AsyncLocal<string?> BridgeAction = new();
    private readonly AppConfig _config;
    private readonly string _root;
    private readonly string _data;
    private readonly SafeLogger _logger;
    private readonly Func<JsonObject, string, XlsxExportResult> _xlsxWriter;

    private DataStore(AppConfig config, Func<JsonObject, string, XlsxExportResult>? xlsxWriter = null)
    {
        _config = config;
        _root = config.NetworkRoot;
        _data = Path.Combine(_root, config.DataFolder);
        _logger = new SafeLogger(Path.Combine(_root, config.LogsFolder), config.LogMaxFileSizeBytes, config.LogMaxHistoryFiles);
        _xlsxWriter = xlsxWriter ?? XlsxExporter.Write;
    }

    public static DataStore Create()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "config.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Falta config.json junto al ejecutable.");
        var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("config.json no es válido.");
        return FromConfig(config);
    }

    internal static DataStore FromConfig(AppConfig config, Func<JsonObject, string, XlsxExportResult>? xlsxWriter = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!Directory.Exists(config.NetworkRoot) || !Directory.Exists(Path.Combine(config.NetworkRoot, config.DataFolder))) throw new DirectoryNotFoundException("No se puede acceder a la carpeta de datos configurada.");
        return new DataStore(config, xlsxWriter);
    }

    internal IDisposable BeginBridgeAction(string action)
    {
        var previous = BridgeAction.Value;
        BridgeAction.Value = action;
        return new BridgeActionScope(previous);
    }

    internal void LogBridgeAction(string action, bool success, long durationMs, string? scenarioId = null)
    {
        _logger.Info("bridge.action", bridgeAction: action, scenarioId: scenarioId, result: success ? "success" : "failure", currentRevision: CurrentRevisionForAudit(), durationMs: durationMs);
    }

    internal void LogLifecycleStart() => LogLifecycle("lifecycle.start");

    internal void LogLifecycleClosing() => LogLifecycle("lifecycle.closing");

    internal JsonObject ReportPlanResourceDiagnostic(JsonObject payload)
    {
        var mapId = payload["mapId"]?.GetValue<string>() ?? "unknown";
        var resource = payload["resource"]?.GetValue<string>() ?? "(sin recurso)";
        var result = payload["result"]?.GetValue<string>() ?? payload["error"]?.GetValue<string>() ?? "Plan SVG cargado.";
        _logger.Info("plan.resource.diagnostic", result: result, currentRevision: CurrentRevisionForAudit(), details: new Dictionary<string, object?>
        {
            ["mapId"] = mapId,
            ["resource"] = Path.GetFileName(resource)
        });
        return new JsonObject { ["logged"] = true };
    }

    public JsonObject Load(string? scenarioId = null) => WithLock("load", () => LoadUnlocked(scenarioId));

    public JsonObject RunValidation(string? scenarioId = null) => WithLock("validation.run", () =>
    {
        _logger.Info("validation.started", scenarioId: scenarioId);
        var effective = LoadUnlocked(scenarioId);
        var stopwatch = Stopwatch.StartNew();
        var maps = effective["maps"]?.AsObject() ?? throw new InvalidDataException("Faltan planos."); var assignments = effective["assignments"]?.AsObject() ?? throw new InvalidDataException("Faltan asignaciones."); var results = ValidationEngine.OperationalResults(ValidationEngine.Run(maps, assignments));
        var counts = results.GroupBy(result => result.Severity).ToDictionary(group => group.Key.ToString().ToLowerInvariant(), group => group.Count());
        var summary = new JsonObject { ["total"] = results.Count, ["critical"] = counts.GetValueOrDefault("critical"), ["warning"] = counts.GetValueOrDefault("warning"), ["info"] = counts.GetValueOrDefault("info") };
        _logger.Info("validation.finished", scenarioId: scenarioId, count: results.Count, durationMs: stopwatch.ElapsedMilliseconds, details: new Dictionary<string, object?> { ["critical"] = counts.GetValueOrDefault("critical"), ["warning"] = counts.GetValueOrDefault("warning"), ["info"] = counts.GetValueOrDefault("info") });
        return new JsonObject { ["results"] = new JsonArray(results.Select(ValidationJson).ToArray()), ["summary"] = summary, ["count"] = results.Count, ["durationMs"] = stopwatch.ElapsedMilliseconds };
    });

    private static JsonObject ValidationJson(ValidationResult result) => new()
    {
        ["id"] = result.Id, ["ruleId"] = result.RuleId, ["severity"] = result.Severity.ToString(), ["classification"] = result.Classification.ToString(), ["operational"] = result.IsOperational,
        ["entityType"] = result.EntityType, ["entityId"] = result.EntityId, ["mapId"] = result.MapId,
        ["field"] = result.Field, ["title"] = result.Title, ["message"] = result.Message,
        ["details"] = result.Details, ["relatedEntities"] = new JsonArray(result.RelatedEntityIds.Select(id => (JsonNode?)id).ToArray()),
        ["suggestedAction"] = result.SuggestedAction
    };

    public JsonObject RunSpatialAnalytics(string? scenarioId = null) => WithLock("analytics.run", () =>
    {
        var normalizedScenarioId = NormalizeScenarioId(scenarioId);
        var effective = LoadUnlocked(normalizedScenarioId);
        var maps = effective["maps"]?.AsObject() ?? throw new InvalidDataException("Faltan planos.");
        var assignments = effective["assignments"]?.AsObject() ?? throw new InvalidDataException("Faltan asignaciones.");
        var stopwatch = Stopwatch.StartNew();
        var validation = ValidationEngine.OperationalResults(ValidationEngine.Run(maps, assignments));
        IReadOnlyList<ScenarioDiffChange>? changes = null;
        SpatialAnalyticsReport? baseline = null;
        if (normalizedScenarioId is not null)
        {
            var scenario = FindScenarioUnlocked(normalizedScenarioId);
            var baseState = scenario["base"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
            var draftState = scenario["draft"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
            var baseMaps = baseState["maps"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
            var baseAssignments = baseState["assignments"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
            var baseValidation = ValidationEngine.OperationalResults(ValidationEngine.Run(baseMaps, baseAssignments));
            changes = ScenarioDiffEngine.Compare(baseState, draftState, baseValidation, validation).Changes;
            baseline = SpatialAnalyticsEngine.Analyze(baseMaps, baseAssignments, baseValidation);
        }
        var report = SpatialAnalyticsEngine.Analyze(maps, assignments, validation, changes);
        var durationMs = stopwatch.ElapsedMilliseconds;
        _logger.Info("analytics.finished", scenarioId: normalizedScenarioId, count: report.Totals.Total, durationMs: durationMs, details: new Dictionary<string, object?> { ["occupied"] = report.Totals.Occupied, ["free"] = report.Totals.Free, ["reserved"] = report.Totals.Reserved, ["problems"] = report.Validation.Total, ["scenarioChanges"] = report.Scenario?.TotalChanges ?? 0 });
        return new JsonObject { ["contextScenarioId"] = normalizedScenarioId, ["result"] = SpatialAnalyticsJson(report), ["baseline"] = baseline is null ? null : SpatialAnalyticsJson(baseline), ["durationMs"] = durationMs };
    });

    private static JsonObject SpatialAnalyticsJson(SpatialAnalyticsReport report) => new()
    {
        ["totals"] = SeatMetricsJson(report.Totals),
        ["validation"] = ValidationTotalsJson(report.Validation),
        ["maps"] = new JsonArray(report.Maps.Select(map => new JsonObject { ["mapId"] = map.MapId, ["mapName"] = map.MapName, ["seats"] = SeatMetricsJson(map.Seats), ["validation"] = ValidationTotalsJson(map.Validation) }).ToArray()),
        ["heatmapPoints"] = new JsonArray(report.HeatmapPoints.Select(point => new JsonObject { ["mapId"] = point.MapId, ["mapName"] = point.MapName, ["seatId"] = point.SeatId, ["x"] = point.X, ["y"] = point.Y, ["layer"] = point.Layer, ["value"] = point.Value, ["sourceId"] = point.SourceId }).ToArray()),
        ["scenario"] = report.Scenario is null ? null : new JsonObject { ["totalChanges"] = report.Scenario.TotalChanges, ["mappedChanges"] = report.Scenario.MappedChanges }
    };

    private static JsonObject SeatMetricsJson(SpatialSeatMetrics metrics) => new() { ["total"] = metrics.Total, ["occupied"] = metrics.Occupied, ["free"] = metrics.Free, ["reserved"] = metrics.Reserved, ["occupancyRate"] = metrics.OccupancyRate, ["availabilityRate"] = metrics.AvailabilityRate };
    private static JsonObject ValidationTotalsJson(SpatialValidationTotals metrics) => new() { ["total"] = metrics.Total, ["critical"] = metrics.Critical, ["warning"] = metrics.Warning, ["info"] = metrics.Info };

    public JsonObject RunMovementPlanner(JsonObject payload) => WithLock("planner.run", () =>
    {
        var scenarioId = Text(payload["scenarioId"]);
        var effective = LoadUnlocked(scenarioId.Length == 0 ? null : scenarioId);
        var maps = effective["maps"]?.AsObject() ?? throw new InvalidDataException("Faltan planos.");
        var assignments = effective["assignments"]?.AsObject() ?? throw new InvalidDataException("Faltan asignaciones.");
        var requests = payload["requests"]?.AsArray().OfType<JsonObject>().Select(item => new MovementRequest(Text(item["sourceWorkspaceId"]), Text(item["destinationWorkspaceId"]))).ToArray() ?? [];
        var validation = ValidationEngine.OperationalResults(ValidationEngine.Run(maps, assignments));
        IReadOnlyList<ScenarioDiffChange> scenarioChanges = [];
        if (scenarioId.Length > 0)
        {
            var scenario = FindScenarioUnlocked(scenarioId);
            var baseState = scenario["base"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
            var draftState = scenario["draft"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
            scenarioChanges = ScenarioDiffEngine.Compare(baseState, draftState, ValidationEngine.OperationalResults(ValidationEngine.Run(baseState["maps"]!.AsObject(), baseState["assignments"]!.AsObject())), validation).Changes;
        }
        var plan = MovementPlanner.Run(maps, assignments, requests, validation, scenarioChanges);
        _logger.Info("planner.finished", scenarioId: scenarioId.Length == 0 ? null : scenarioId, count: plan.Proposals.Count, details: new Dictionary<string, object?> { ["requested"] = plan.Summary.Requested, ["blocked"] = plan.Summary.Blocked });
        return MovementPlanJson(plan);
    });

    private static JsonObject MovementPlanJson(MovementPlan plan) => new()
    {
        ["summary"] = new JsonObject { ["requested"] = plan.Summary.Requested, ["planned"] = plan.Summary.Planned, ["blocked"] = plan.Summary.Blocked },
        ["proposals"] = new JsonArray(plan.Proposals.Select(proposal => new JsonObject
        {
            ["id"] = proposal.Id, ["source"] = MovementEndpointJson(proposal.Source), ["destination"] = MovementEndpointJson(proposal.Destination),
            ["relatedProblems"] = new JsonArray(proposal.RelatedProblems.Select(ValidationJson).ToArray()),
            ["relatedScenarioChanges"] = new JsonArray(proposal.RelatedScenarioChanges.Select(ScenarioDiffJson).ToArray())
        }).ToArray()),
        ["issues"] = new JsonArray(plan.Issues.Select(issue => new JsonObject { ["id"] = issue.Id, ["code"] = issue.Code, ["message"] = issue.Message, ["sourceWorkspaceId"] = issue.SourceWorkspaceId, ["destinationWorkspaceId"] = issue.DestinationWorkspaceId }).ToArray())
    };

    private static JsonObject MovementEndpointJson(MovementEndpoint endpoint) => new()
    {
        ["workspaceId"] = endpoint.WorkspaceId, ["mapId"] = endpoint.MapId, ["displayLocation"] = endpoint.DisplayLocation,
        ["personId"] = endpoint.PersonId, ["deviceId"] = endpoint.DeviceId, ["roseta"] = endpoint.Roseta
    };

    public JsonObject CreateScenarioFromMovementPlan(JsonObject payload) => WithLock("planner.scenario.create", () =>
    {
        EnsureWritable();
        var name = Required(payload, "name", "El escenario necesita un nombre.");
        var requests = payload["requests"]?.AsArray().OfType<JsonObject>().Select(item => new MovementRequest(Text(item["sourceWorkspaceId"]), Text(item["destinationWorkspaceId"]))).ToArray() ?? [];
        if (requests.Length == 0) throw new InvalidDataException("El plan no contiene movimientos.");
        var real = RealStateUnlocked();
        var plan = MovementPlanner.Run(real["maps"]!.AsObject(), real["assignments"]!.AsObject(), requests);
        if (plan.Issues.Count > 0 || plan.Proposals.Count != requests.Length) throw new InvalidDataException("El plan contiene movimientos bloqueados.");
        var draft = (JsonObject)real.DeepClone();
        var list = draft["assignments"]?["assignments"]?.AsArray() ?? new JsonArray();
        var people = ReadRequired("people.json");
        var devices = ReadRequired("devices.json");
        var scenarios = ScenariosUnlocked(); var id = $"scenario-{Guid.NewGuid():N}";
        var operations = new JsonArray();
        foreach (var proposal in plan.Proposals)
        {
            var source = list.OfType<JsonObject>().FirstOrDefault(item => Text(item["workstationId"]) == proposal.Source.WorkspaceId);
            var legacySource = source is null;
            JsonObject assignment;
            if (legacySource)
            {
                var sourceSeat = Seat(draft, proposal.Source.MapId, proposal.Source.WorkspaceId);
                var personId = LegacyPersonId(sourceSeat, people);
                var deviceId = LegacyDeviceId(sourceSeat, devices);
                sourceSeat["personId"] = null;
                if (deviceId is not null) sourceSeat["deviceName"] = null;
                assignment = LegacyDestinationAssignment(proposal, personId, deviceId);
            }
            else
            {
                assignment = (JsonObject)source!.DeepClone();
                assignment["workstationId"] = proposal.Destination.WorkspaceId;
                assignment["updatedAt"] = DateTimeOffset.UtcNow.ToString("O"); assignment["updatedBy"] = Environment.UserName;
            }
            Remove(list, item => Text(item?["workstationId"]) == proposal.Source.WorkspaceId || Text(item?["workstationId"]) == proposal.Destination.WorkspaceId);
            list.Add(assignment);
            operations.Add(MovementOperationJson(id, proposal, legacySource));
        }
        draft["assignments"]!["assignments"] = list;
        var item = new JsonObject { ["id"] = id, ["name"] = name, ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"), ["createdBy"] = Environment.UserName, ["baseRevision"] = CurrentRevisionUnlocked(), ["baseVersion"] = real["version"]?.DeepClone(), ["base"] = real.DeepClone(), ["draft"] = draft, ["operations"] = operations, ["undo"] = new JsonArray() };
        var scenarioList = scenarios["scenarios"]?.AsArray() ?? new JsonArray(); scenarioList.Add(item); scenarios["scenarios"] = scenarioList; StampRevision(scenarios, CurrentRevisionUnlocked()); WriteAtomic(ScenariosPath, scenarios);
        _logger.Info("planner.scenario.created", scenarioId: id, count: plan.Proposals.Count);
        return new JsonObject { ["id"] = id, ["scenarioId"] = id, ["planned"] = plan.Proposals.Count };
    });

    public JsonObject CreateScenario(JsonObject payload) => WithLock("scenario.create", () =>
    {
        EnsureWritable();
        var name = Required(payload, "name", "El escenario necesita un nombre.");
        var real = RealStateUnlocked();
        var scenarios = ScenariosUnlocked();
        var id = $"scenario-{Guid.NewGuid():N}";
        var item = new JsonObject
        {
            ["id"] = id,
            ["name"] = name,
            ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["createdBy"] = Environment.UserName,
            ["baseRevision"] = CurrentRevisionUnlocked(),
            ["baseVersion"] = real["version"]?.DeepClone(),
            ["base"] = real.DeepClone(),
            ["draft"] = real.DeepClone(),
            ["undo"] = new JsonArray()
        };
        var list = scenarios["scenarios"]?.AsArray() ?? new JsonArray();
        list.Add(item);
        scenarios["scenarios"] = list;
        StampRevision(scenarios, CurrentRevisionUnlocked());
        WriteAtomic(ScenariosPath, scenarios);
        return new JsonObject { ["id"] = id, ["scenarioId"] = id };
    });

    public JsonObject DeleteScenario(JsonObject payload) => WithLock("scenario.delete", () =>
    {
        EnsureWritable();
        var id = Required(payload, "scenarioId", "Selecciona un escenario.");
        if (string.Equals(id, "real", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("La realidad principal no se puede eliminar.");
        var document = ScenariosUnlocked();
        var list = document["scenarios"]?.AsArray() ?? new JsonArray();
        var scenario = list.OfType<JsonObject>().FirstOrDefault(item => Text(item["id"]) == id) ?? throw new InvalidDataException("Escenario inexistente.");
        if (scenario["isPrimary"]?.GetValue<bool>() == true) throw new InvalidOperationException("El escenario principal no se puede eliminar.");
        Remove(list, item => Text(item?["id"]) == id);
        document["scenarios"] = list;
        StampRevision(document, CurrentRevisionUnlocked());
        WriteAtomic(ScenariosPath, document);
        return new JsonObject { ["deleted"] = id };
    });

    public JsonObject SaveAssignment(JsonObject payload, bool delete) => WithLock(delete ? "assignment.delete" : "assignment.save", () =>
    {
        EnsureWritable();
        if (delete)
        {
            var request = DeleteAssignmentRequest.From(payload);
            if (request.ScenarioId is not null) return MutateScenarioUnlocked(request.ScenarioId, draft => DeleteAssignment(draft, request));
            var state = RealStateUnlocked();
            DeleteAssignment(state, request);
            CommitRealUnlocked(state, "Asignación eliminada", request.WorkstationId!);
            return new JsonObject { ["ok"] = true };
        }

        var save = SaveAssignmentRequest.From(payload);
        if (save.ScenarioId is not null)
        {
            var draft = FindScenarioUnlocked(save.ScenarioId)["draft"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
            var warnings = ValidateAssignment(draft, save);
            var result = MutateScenarioUnlocked(save.ScenarioId, state => SetAssignment(state, save));
            result["warnings"] = WarningArray(warnings);
            return result;
        }
        var real = RealStateUnlocked();
        var realWarnings = ValidateAssignment(real, save);
        SetAssignment(real, save);
        CommitRealUnlocked(real, "Asignación guardada", save.WorkstationId!);
        return new JsonObject { ["ok"] = true, ["warnings"] = WarningArray(realWarnings) };
    });

    public JsonObject BulkUpdateAssignments(JsonObject payload) => WithLock("workspace.bulk", () =>
    {
        EnsureWritable();
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
            var scenario = FindScenarioUnlocked(request.ScenarioId);
            var draft = scenario["draft"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
            var preview = (JsonObject)draft.DeepClone();
            var updated = Apply(preview);
            if (updated == 0) return BulkAssignmentResult(request, 0);
            MutateScenarioUnlocked(request.ScenarioId, state => Apply(state));
            return BulkAssignmentResult(request, updated);
        }

        var real = RealStateUnlocked();
        var realUpdated = Apply(real);
        if (realUpdated == 0) return BulkAssignmentResult(request, 0);
        var title = request.Status == "reserved" ? "Puestos reservados" : "Reservas retiradas";
        CommitRealUnlocked(real, title, $"{realUpdated} puestos");
        return BulkAssignmentResult(request, realUpdated);
    });

    private static JsonObject BulkAssignmentResult(BulkAssignmentRequest request, int updated) => new()
    {
        ["ok"] = true,
        ["updated"] = updated,
        ["requested"] = request.WorkstationIds?.Count ?? 0,
        ["status"] = request.Status,
        ["action"] = request.Status == "reserved" ? "reserve" : "removeReservation",
        ["noOp"] = updated == 0
    };

    public JsonObject CreateManagedArea(JsonObject payload) => WithLock("managed-area.create", () =>
    {
        EnsureWritable();
        ValidatePayloadFields(payload, "createManagedArea", ["id", "areaId", "mapId", "name", "workspaceIds", "moveWorkspaceIds"]);
        var id = FirstText(payload, "id", "areaId");
        if (id.Length == 0) id = $"managed-area-{Guid.NewGuid():N}";
        var mapId = Required(payload, "mapId", "La Managed Area necesita un plano.");
        var name = Required(payload, "name", "La Managed Area necesita un nombre.");
        var workspaceIds = PayloadIds(payload, "workspaceIds", required: false);
        var moveWorkspaceIds = PayloadIds(payload, "moveWorkspaceIds", required: false);
        return MutateManagedAreasUnlocked(source => moveWorkspaceIds.Length == 0
            ? ManagedAreas.Create(source.Document, source.Maps, id, mapId, name, workspaceIds)
            : ManagedAreas.CreateWithMoves(source.Document, source.Maps, id, mapId, name, workspaceIds, moveWorkspaceIds), "Cluster creado", name);
    });

    public JsonObject RenameManagedArea(JsonObject payload) => WithLock("managed-area.rename", () =>
    {
        EnsureWritable();
        ValidatePayloadFields(payload, "renameManagedArea", ["areaId", "id", "name"]);
        var areaId = RequiredFirst(payload, "Selecciona una Managed Area.", "areaId", "id");
        var name = Required(payload, "name", "La Managed Area necesita un nombre.");
        return MutateManagedAreasUnlocked(source => ManagedAreas.Rename(source.Document, source.Maps, areaId, name), "Managed Area renombrada", name);
    });

    public JsonObject AddManagedAreaWorkspaces(JsonObject payload) => WithLock("managed-area.workspace.add", () =>
    {
        EnsureWritable();
        ValidatePayloadFields(payload, "addManagedAreaWorkspaces", ["areaId", "id", "workspaceIds"]);
        var areaId = RequiredFirst(payload, "Selecciona una Managed Area.", "areaId", "id");
        var workspaceIds = PayloadIds(payload, "workspaceIds", required: true);
        return MutateManagedAreasUnlocked(source => ManagedAreas.AddWorkspaces(source.Document, source.Maps, areaId, workspaceIds), "Puestos añadidos al cluster", $"{workspaceIds.Length} puestos");
    });

    public JsonObject RemoveManagedAreaWorkspaces(JsonObject payload) => WithLock("managed-area.workspace.remove", () =>
    {
        EnsureWritable();
        ValidatePayloadFields(payload, "removeManagedAreaWorkspaces", ["areaId", "id", "workspaceIds"]);
        var areaId = RequiredFirst(payload, "Selecciona una Managed Area.", "areaId", "id");
        var workspaceIds = PayloadIds(payload, "workspaceIds", required: true);
        return MutateManagedAreasUnlocked(source => ManagedAreas.RemoveWorkspaces(source.Document, source.Maps, areaId, workspaceIds), "Puestos retirados del cluster", $"{workspaceIds.Length} puestos");
    });

    public JsonObject MoveManagedAreaWorkspaces(JsonObject payload) => WithLock("managed-area.workspace.move", () =>
    {
        EnsureWritable();
        ValidatePayloadFields(payload, "moveManagedAreaWorkspaces", ["fromAreaId", "sourceAreaId", "toAreaId", "targetAreaId", "workspaceIds"]);
        var fromAreaId = RequiredFirst(payload, "Selecciona la Managed Area de origen.", "fromAreaId", "sourceAreaId");
        var toAreaId = RequiredFirst(payload, "Selecciona la Managed Area de destino.", "toAreaId", "targetAreaId");
        var workspaceIds = PayloadIds(payload, "workspaceIds", required: true);
        return MutateManagedAreasUnlocked(source => ManagedAreas.MoveWorkspaces(source.Document, source.Maps, fromAreaId, toAreaId, workspaceIds), "Puestos movidos entre Managed Areas", $"{workspaceIds.Length} puestos");
    });

    public JsonObject MergeManagedAreas(JsonObject payload) => WithLock("managed-area.merge", () =>
    {
        EnsureWritable();
        ValidatePayloadFields(payload, "mergeManagedAreas", ["targetAreaId", "areaId", "sourceAreaIds"]);
        var targetAreaId = RequiredFirst(payload, "Selecciona la Managed Area de destino.", "targetAreaId", "areaId");
        var sourceAreaIds = PayloadIds(payload, "sourceAreaIds", required: true);
        return MutateManagedAreasUnlocked(source => ManagedAreas.Merge(source.Document, source.Maps, targetAreaId, sourceAreaIds), "Clusters fusionados", $"{sourceAreaIds.Length} clusters en {targetAreaId}");
    });

    public JsonObject DissolveManagedArea(JsonObject payload) => WithLock("managed-area.dissolve", () =>
    {
        EnsureWritable();
        ValidatePayloadFields(payload, "dissolveManagedArea", ["areaId", "id"]);
        var areaId = RequiredFirst(payload, "Selecciona una Managed Area.", "areaId", "id");
        var maps = ReadRequired("maps.json");
        var current = ManagedAreas.Normalize(ReadOptional(ManagedAreas.FileName), maps);
        var area = current["areas"]!.AsArray().OfType<JsonObject>().FirstOrDefault(item => Text(item["id"]) == areaId) ?? throw new InvalidDataException($"La Managed Area {areaId} no existe.");
        var name = Text(area["name"]);
        var count = area["workspaceIds"]?.AsArray().Count ?? 0;
        return MutateManagedAreasUnlocked(source => ManagedAreas.Dissolve(source.Document, source.Maps, areaId), "Cluster disuelto", $"{name} · {count} puestos conservados");
    });

    public JsonObject DeleteManagedAreaAndMoveWorkspaces(JsonObject payload) => WithLock("managed-area.delete-and-move", () =>
    {
        EnsureWritable();
        ValidatePayloadFields(payload, "deleteManagedAreaAndMoveWorkspaces", ["areaId", "sourceAreaId", "targetAreaId", "toAreaId"]);
        var sourceAreaId = RequiredFirst(payload, "Selecciona la Managed Area que se eliminará.", "sourceAreaId", "areaId");
        var targetAreaId = RequiredFirst(payload, "Selecciona la Managed Area de destino.", "targetAreaId", "toAreaId");
        return MutateManagedAreasUnlocked(source => ManagedAreas.DeleteAndMove(source.Document, source.Maps, sourceAreaId, targetAreaId), "Managed Area eliminada y puestos movidos", $"{sourceAreaId} → {targetAreaId}");
    });

    public JsonObject SavePosition(JsonObject payload) => WithLock("seat.move", () =>
    {
        EnsureWritable();
        var request = SavePositionRequest.From(payload);
        if (request.ScenarioId is not null) return MutateScenarioUnlocked(request.ScenarioId, draft => SetPosition(draft, request));
        var state = RealStateUnlocked();
        SetPosition(state, request);
        CommitRealUnlocked(state, "Puesto movido", request.SeatId!);
        return new JsonObject { ["ok"] = true };
    });

    public JsonObject CreateSeat(JsonObject payload) => WithLock("seat.create", () =>
    {
        EnsureWritable();
        var request = CreateSeatRequest.From(payload);
        string id = "";
        if (request.ScenarioId is not null)
        {
            if (request.TargetManagedAreaId is not null) throw new InvalidOperationException("No se puede crear un puesto dentro de una zona gestionada desde un escenario: la pertenencia debe confirmarse en la realidad.");
            MutateScenarioUnlocked(request.ScenarioId, draft => id = AddSeat(draft, request));
            return new JsonObject { ["id"] = id, ["mapId"] = request.MapId };
        }

        var state = RealStateUnlocked();
        if (request.TargetManagedAreaId is null)
        {
            id = AddSeat(state, request);
            CommitRealUnlocked(state, "Puesto creado", id);
            return new JsonObject { ["id"] = id, ["mapId"] = request.MapId };
        }

        var managedAreas = ManagedAreas.Normalize(ReadOptional(ManagedAreas.FileName), state["maps"]!.AsObject());
        var targetArea = managedAreas["areas"]!.AsArray().OfType<JsonObject>().FirstOrDefault(area => Text(area["id"]) == request.TargetManagedAreaId)
            ?? throw new InvalidDataException("La zona gestionada ya no existe.");
        if (Text(targetArea["mapId"]) != request.MapId) throw new InvalidDataException("La zona gestionada pertenece a otro plano.");

        var targetName = Text(targetArea["name"]);
        id = AddSeat(state, request);
        var membership = ManagedAreas.AddWorkspaces(managedAreas, state["maps"]!.AsObject(), request.TargetManagedAreaId, [id]);
        Bump(state["assignments"]!.AsObject());
        Bump(membership.Document);
        var documents = RealDocuments(state);
        documents[ManagedAreas.FileName] = membership.Document;
        ExecuteTransactionUnlocked(
            documents,
            RealFiles.Append(ManagedAreas.FileName),
            "Antes de puesto creado en zona",
            "Puesto creado en zona",
            $"{id} creado en {targetName}",
            CurrentRevisionUnlocked(),
            seatId: id);
        return new JsonObject { ["id"] = id, ["mapId"] = request.MapId, ["targetManagedAreaId"] = request.TargetManagedAreaId };
    });

    public JsonObject DeleteSeat(JsonObject payload) => WithLock("seat.delete", () =>
    {
        EnsureWritable();
        var request = DeleteSeatRequest.From(payload);
        if (request.ScenarioId is not null) return MutateScenarioUnlocked(request.ScenarioId, draft => RemoveSeat(draft, request));
        var state = RealStateUnlocked();
        EnsureWorkspaceIsNotManagedUnlocked(state["maps"]!.AsObject(), request.MapId!, request.SeatId!);
        RemoveSeat(state, request);
        CommitRealUnlocked(state, "Puesto eliminado", request.SeatId!);
        return new JsonObject { ["ok"] = true };
    });

    public JsonObject GetScenarioDiff(JsonObject payload) => WithLock("scenario.diff", () =>
    {
        var scenarios = ScenariosUnlocked();
        var scenario = scenarios["scenarios"]?.AsArray().OfType<JsonObject>().FirstOrDefault(item => Text(item["id"]) == Required(payload, "scenarioId", "Selecciona un escenario.")) ?? throw new InvalidDataException("Escenario inexistente.");
        if (scenario["draft"] is null)
        {
            MigrateLegacyScenario(scenario, RealStateUnlocked());
            if (!_config.ReadOnly)
            {
                StampRevision(scenarios, CurrentRevisionUnlocked());
                WriteAtomic(ScenariosPath, scenarios);
            }
        }
        var baseState = scenario["base"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
        var draft = scenario["draft"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
        return ScenarioComparisonJson(CompareScenario(baseState, draft, ScenarioOperations(scenario)));
    });

    public JsonObject ApplyScenario(JsonObject payload) => WithLock("scenario.apply", () =>
    {
        EnsureWritable();
        var scenarioId = Required(payload, "scenarioId", "Selecciona un escenario.");
        var scenarios = ScenariosUnlocked();
        var scenario = scenarios["scenarios"]?.AsArray().OfType<JsonObject>().FirstOrDefault(item => Text(item["id"]) == scenarioId) ?? throw new InvalidDataException("Escenario inexistente.");
        if (!TryRevision(scenario["baseRevision"], out var baseRevision)) throw new InvalidOperationException(LegacyScenarioApplyError);
        var sourceRevision = CurrentRevisionUnlocked();
        if (baseRevision != sourceRevision) throw new InvalidOperationException("La realidad cambió desde la creación del escenario. Recarga y revisa el diff.");
        var baseState = scenario["base"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
        var draft = scenario["draft"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
        var operations = ScenarioOperations(scenario);
        var chosen = payload["changeIds"]?.AsArray().Select(Text).ToHashSet(StringComparer.Ordinal) ?? [];
        var pending = Diff(baseState, draft, operations).OfType<JsonObject>().ToList();
        var pendingIds = pending.Select(change => Text(change["id"])).ToHashSet(StringComparer.Ordinal);
        if (operations.Any(operation => operation.Atomic && operation.Members.Any(member => pendingIds.Contains(member) && chosen.Contains(member)) && operation.Members.Any(member => pendingIds.Contains(member) && !chosen.Contains(member))))
            throw new InvalidDataException("atomic-operation-incomplete");
        var selected = pending.Where(change => chosen.Contains(Text(change["id"]))).ToList();
        if (selected.Count == 0) throw new InvalidDataException("Selecciona cambios pendientes para aplicar.");
        var real = RealStateUnlocked();

        foreach (var change in selected)
        {
            ApplyChange(real, change, draft);
            ApplyChange(baseState, change, draft);
        }
        Bump(real["assignments"]!.AsObject());
        var destinationRevision = checked(sourceRevision + 1);
        scenario["base"] = baseState;
        scenario["baseVersion"] = real["assignments"]?["version"]?.DeepClone();
        scenario["baseRevision"] = destinationRevision;
        scenario["updatedAt"] = DateTimeOffset.UtcNow.ToString("O");
        scenario["updatedBy"] = Environment.UserName;
        ValidateManagedAreasAgainstMapsUnlocked(real["maps"]!.AsObject());
        var documents = RealDocuments(real);
        documents["scenarios.json"] = scenarios;
        var scenarioName = Text(scenario["name"]);
        ExecuteTransactionUnlocked(documents, documents.Keys.Append("events.json"), "Antes de aplicar escenario " + scenarioName, "Escenario aplicado", scenarioName, sourceRevision);
        return new JsonObject { ["applied"] = selected.Count, ["remaining"] = Diff(baseState, draft, operations).Count };
    });

    public JsonObject GetEvents() => WithLock("events.read", () => new JsonObject { ["events"] = (ReadOptional("events.json")?["events"]?.DeepClone() ?? new JsonArray()) });

    public JsonObject GetBackups() => WithLock("backup.list", () =>
    {
        var backups = new JsonArray();
        foreach (var container in BackupContainersUnlocked()) backups.Add(container.Manifest);
        return new JsonObject { ["backups"] = backups };
    });

    public JsonObject GetBackupRetentionReport() => WithLock("backup.retention.report", () =>
    {
        var mode = BackupRetention.ParseMode(_config.BackupRetentionMode);
        if (mode != BackupRetentionMode.Report) throw new InvalidOperationException("El informe de retención requiere backupRetentionMode=report.");

        var report = BackupRetentionReport.Build(
            BackupContainersUnlocked(),
            ReadOptional("events.json")?["events"]?.AsArray(),
            DateTimeOffset.UtcNow);
        var document = RetentionReportDocument(report);
        WriteRetentionReport(document, report.TotalBackups, report.ReclaimableBytes);
        return document;
    });

    public JsonObject GetIntegrityReport() => WithLock("integrity.report", () =>
    {
        var document = IntegrityReport.Build(
            ReadRequired("maps.json"),
            ReadRequired("assignments.json"),
            ReadRequired("positions.json"));
        WriteIntegrityReport(document);
        return document;
    });

    public JsonObject RestoreBackup(JsonObject payload) => WithLock("backup.restore", () =>
    {
        EnsureWritable();
        var id = Required(payload, "backupId", "Backup inválido.");
        var folder = BackupPath(id);
        if (!BackupExists(folder)) throw new DirectoryNotFoundException("No existe el backup seleccionado.");
        var files = BackupFiles(folder);
        var restoredFiles = files.Where(file => UserRestoreFiles.Contains(file, StringComparer.OrdinalIgnoreCase)).ToArray();
        var documents = LoadBackupDocuments(folder, restoredFiles);
        var restoredMaps = documents.GetValueOrDefault("maps.json") ?? ReadRequired("maps.json");
        var restoredManagedAreas = documents.GetValueOrDefault(ManagedAreas.FileName) ?? ReadOptional(ManagedAreas.FileName) ?? ManagedAreas.EmptyDocument();
        ManagedAreas.Normalize(restoredManagedAreas, restoredMaps);
        ExecuteTransactionUnlocked(documents, restoredFiles.Union(["events.json"], StringComparer.OrdinalIgnoreCase), "Antes de restaurar " + id, "Backup restaurado", id, CurrentRevisionUnlocked());
        return new JsonObject { ["ok"] = true };
    });

    public JsonObject GetUndoPreview(JsonObject payload) => WithLock("undo.preview", () =>
    {
        var scenarioId = Text(payload["scenarioId"]);
        if (scenarioId.Length > 0)
        {
            var scenario = FindScenarioUnlocked(scenarioId);
            var undo = scenario["undo"]?.AsArray() ?? new JsonArray();
            if (undo.Count == 0) throw new InvalidOperationException("No hay cambios del escenario para deshacer.");
            var current = scenario["draft"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
            var previous = undo[undo.Count - 1]?.AsObject() ?? throw new InvalidDataException("Historial de escenario corrupto.");
            return new JsonObject { ["scope"] = "scenario", ["title"] = $"Escenario: {Text(scenario["name"])}", ["description"] = "Se revertirá el último cambio de este borrador. La realidad confirmada no se modifica.", ["changes"] = Diff(previous, current) };
        }
        var events = ReadOptional("events.json")?["events"]?.AsArray().OfType<JsonObject>().Reverse().ToList() ?? [];
        var last = events.FirstOrDefault(item => Text(item["backupId"]).Length > 0 && Text(item["undoOf"]).Length == 0 && Text(item["undoneAt"]).Length == 0)
            ?? throw new InvalidOperationException("No hay más cambios reales reversibles.");
        return new JsonObject { ["scope"] = "real", ["title"] = Text(last["title"]), ["description"] = Text(last["description"]), ["createdAt"] = last["createdAt"]?.DeepClone(), ["createdBy"] = last["createdBy"]?.DeepClone(), ["changes"] = new JsonArray() };
    });

    public JsonObject UndoLastChange(JsonObject payload) => WithLock("undo.apply", () =>
    {
        var scenarioId = Text(payload["scenarioId"]);
        if (scenarioId.Length > 0)
        {
            EnsureWritable();
            var document = ScenariosUnlocked();
            var scenario = document["scenarios"]?.AsArray().OfType<JsonObject>().FirstOrDefault(item => Text(item["id"]) == scenarioId) ?? throw new InvalidDataException("Escenario inexistente.");
            var undo = scenario["undo"]?.AsArray() ?? new JsonArray();
            if (undo.Count == 0) throw new InvalidOperationException("No hay cambios del escenario para deshacer.");
            scenario["draft"] = undo[undo.Count - 1]?.DeepClone();
            undo.RemoveAt(undo.Count - 1);
            scenario["undo"] = undo;
            scenario["updatedAt"] = DateTimeOffset.UtcNow.ToString("O");
            scenario["updatedBy"] = Environment.UserName;
            StampRevision(document, CurrentRevisionUnlocked());
            WriteAtomic(ScenariosPath, document);
            return new JsonObject { ["ok"] = true, ["scope"] = "scenario" };
        }

        EnsureWritable();
        var currentEvents = ReadOptional("events.json") ?? New("events");
        var currentEntries = currentEvents["events"]?.AsArray().OfType<JsonObject>().ToList() ?? [];
        var last = currentEntries.LastOrDefault(item => Text(item["backupId"]).Length > 0 && Text(item["undoOf"]).Length == 0 && Text(item["undoneAt"]).Length == 0)
            ?? throw new InvalidOperationException("No hay más cambios reales reversibles.");
        var backupId = Text(last["backupId"]);
        var folder = BackupPath(backupId);
        if (!BackupExists(folder)) throw new DirectoryNotFoundException("No está disponible el backup del último cambio.");
        var files = BackupTransactionFiles(folder);
        var restoredFiles = files.Where(file => !string.Equals(file, "events.json", StringComparison.OrdinalIgnoreCase)).ToArray();
        var documents = LoadBackupDocuments(folder, restoredFiles);
        last["undoneAt"] = DateTimeOffset.UtcNow.ToString("O");
        documents["events.json"] = currentEvents;
        ExecuteTransactionUnlocked(documents, restoredFiles.Union(["events.json"], StringComparer.OrdinalIgnoreCase), "Antes de deshacer " + Text(last["title"]), "Cambio deshecho", Text(last["title"]), CurrentRevisionUnlocked(), Text(last["id"]));
        return new JsonObject { ["ok"] = true, ["scope"] = "real" };
    });

    public JsonObject ExportExcel(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) throw new DirectoryNotFoundException("La carpeta de exportación no está disponible.");
        var (snapshot, revision) = WithLock("export.snapshot", () => (LoadUnlocked(), CurrentRevisionUnlocked()));
        var path = Path.Combine(folder, $"Inventario parcheo de campo - {DateTime.Now:yyyy-MM-ddTHH-mm-ss-fff}.xlsx");
        var export = _xlsxWriter(snapshot, path);
        _logger.Info("export.excel", currentRevision: revision, exportPath: path, count: export.RosetasRowsFilled, details: new Dictionary<string, object?>
        {
            ["rosetasTotal"] = export.RenderedRows,
            ["rosetasFromTemplate"] = export.RosetasFromTemplate,
            ["rosetasFromPlan"] = export.RosetasFromPlan,
            ["rosetasInBoth"] = export.RosetasInBoth,
            ["rosetasOnlyFromPlan"] = export.RosetasOnlyFromPlan,
            ["renderedRows"] = export.RenderedRows,
            ["duplicateRosetas"] = export.DuplicateRosetas,
            ["templateRowsWithoutRoseta"] = export.TemplateRowsWithoutRoseta,
            ["templateRowsSkippedInvalidRoseta"] = export.TemplateRowsSkippedInvalidRoseta,
            ["duplicateRosetaIds"] = export.DuplicateRosetaIds,
            ["duplicateRosetaWorkstations"] = export.DuplicateRosetaWorkstations
        });
        string? openFolderError = null;
        try { Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true }); }
        catch (Exception ex) { openFolderError = ex.Message; }
        return new JsonObject { ["path"] = path, ["folder"] = folder, ["rosetasRowsFilled"] = export.RosetasRowsFilled, ["rosetasTotal"] = export.RenderedRows, ["rosetasFromTemplate"] = export.RosetasFromTemplate, ["rosetasFromPlan"] = export.RosetasFromPlan, ["rosetasInBoth"] = export.RosetasInBoth, ["rosetasOnlyFromPlan"] = export.RosetasOnlyFromPlan, ["renderedRows"] = export.RenderedRows, ["duplicateRosetas"] = export.DuplicateRosetas, ["openFolderError"] = openFolderError };
    }

    private JsonObject LoadUnlocked(string? scenarioId = null)
    {
        var real = RealStateUnlocked();
        var scenarios = ScenariosUnlocked();
        JsonObject state = real;
        JsonObject? active = null;
        if (!string.IsNullOrWhiteSpace(scenarioId))
        {
            active = scenarios["scenarios"]?.AsArray().OfType<JsonObject>().FirstOrDefault(x => Text(x["id"]) == scenarioId) ?? throw new InvalidDataException("Escenario inexistente.");
            if (active["draft"] is null)
            {
                MigrateLegacyScenario(active, real);
                if (!_config.ReadOnly)
                {
                    StampRevision(scenarios, CurrentRevisionUnlocked());
                    WriteAtomic(ScenariosPath, scenarios);
                }
            }
            state = (JsonObject)(active["draft"]?.DeepClone() ?? throw new InvalidDataException("El escenario no tiene borrador."));
        }
        return Package(state, scenarios, active);
    }

    private JsonObject RealStateUnlocked()
    {
        var maps = ReadRequired("maps.json");
        var positions = ReadOptional("positions.json")?["positions"]?.AsArray() ?? new JsonArray();
        var bySeat = positions.OfType<JsonObject>().ToDictionary(p => Text(p["mapId"]) + "|" + Text(p["seatId"]), p => p);
        foreach (var map in maps["maps"]?.AsArray().OfType<JsonObject>() ?? []) foreach (var seat in map["seats"]?.AsArray().OfType<JsonObject>() ?? [])
            if (bySeat.TryGetValue(Text(map["id"]) + "|" + Text(seat["id"]), out var position)) { seat["x"] = position["x"]?.DeepClone(); seat["y"] = position["y"]?.DeepClone(); }
        return new JsonObject { ["maps"] = maps, ["assignments"] = ReadOptional("assignments.json") ?? New("assignments"), ["positions"] = positions.DeepClone(), ["version"] = ReadOptional("assignments.json")?["version"]?.DeepClone() ?? 0 };
    }

    private JsonObject MutateScenarioUnlocked(string id, Action<JsonObject> mutation)
    {
        var document = ScenariosUnlocked();
        var scenario = document["scenarios"]?.AsArray().OfType<JsonObject>().FirstOrDefault(item => Text(item["id"]) == id) ?? throw new InvalidDataException("Escenario inexistente.");
        var draft = scenario["draft"]?.AsObject() ?? throw new InvalidDataException("Escenario corrupto.");
        var undo = scenario["undo"]?.AsArray() ?? new JsonArray();
        undo.Add(draft.DeepClone());
        while (undo.Count > 50) undo.RemoveAt(0);
        scenario["undo"] = undo;
        mutation(draft);
        scenario["updatedAt"] = DateTimeOffset.UtcNow.ToString("O");
        scenario["updatedBy"] = Environment.UserName;
        StampRevision(document, CurrentRevisionUnlocked());
        WriteAtomic(ScenariosPath, document);
        return new JsonObject { ["ok"] = true };
    }

    private void CommitRealUnlocked(JsonObject state, string action, string description)
    {
        Bump(state["assignments"]!.AsObject());
        ExecuteTransactionUnlocked(RealDocuments(state), RealFiles, "Antes de " + action, action, description, CurrentRevisionUnlocked(), seatId: description);
    }

    private JsonObject MutateManagedAreasUnlocked(Func<(JsonObject Document, JsonObject Maps), ManagedAreaMutation> mutation, string eventTitle, string eventDescription)
    {
        var maps = ReadRequired("maps.json");
        var current = ManagedAreas.Normalize(ReadOptional(ManagedAreas.FileName), maps);
        var result = mutation((current, maps));
        if (!result.Changed) return ManagedAreaResult(result, noOp: true);

        Bump(result.Document);
        ExecuteTransactionUnlocked(
            new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase) { [ManagedAreas.FileName] = result.Document },
            [ManagedAreas.FileName],
            "Antes de " + eventTitle,
            eventTitle,
            eventDescription,
            CurrentRevisionUnlocked());
        return ManagedAreaResult(result, noOp: false);
    }

    private static JsonObject ManagedAreaResult(ManagedAreaMutation mutation, bool noOp) => new()
    {
        ["ok"] = true,
        ["noOp"] = noOp,
        ["areaIds"] = new JsonArray(mutation.AreaIds.Select(id => (JsonNode?)id).ToArray()),
        ["workspaceIds"] = new JsonArray(mutation.WorkspaceIds.Select(id => (JsonNode?)id).ToArray()),
        ["managedAreas"] = mutation.Document.DeepClone()
    };

    private void EnsureWorkspaceIsNotManagedUnlocked(JsonObject maps, string mapId, string workspaceId)
    {
        var document = ReadOptional(ManagedAreas.FileName);
        if (document is not null && ManagedAreas.ContainsWorkspace(document, maps, mapId, workspaceId))
            throw new InvalidOperationException($"El puesto {workspaceId} pertenece a una Managed Area. Retíralo o disuelve el área antes de eliminarlo.");
    }

    private void ValidateManagedAreasAgainstMapsUnlocked(JsonObject maps)
    {
        var document = ReadOptional(ManagedAreas.FileName);
        if (document is not null) ManagedAreas.Normalize(document, maps);
    }

    private Dictionary<string, JsonObject> RealDocuments(JsonObject state)
    {
        var positions = New("positions");
        foreach (var map in state["maps"]?["maps"]?.AsArray().OfType<JsonObject>() ?? []) foreach (var seat in map["seats"]?.AsArray().OfType<JsonObject>() ?? [])
            (positions["positions"] as JsonArray)!.Add(new JsonObject { ["mapId"] = map["id"]?.DeepClone(), ["seatId"] = seat["id"]?.DeepClone(), ["x"] = seat["x"]?.DeepClone(), ["y"] = seat["y"]?.DeepClone(), ["updatedAt"] = DateTimeOffset.UtcNow.ToString("O"), ["updatedBy"] = Environment.UserName });
        return new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase)
        {
            ["maps.json"] = state["maps"]!.AsObject(),
            ["assignments.json"] = state["assignments"]!.AsObject(),
            ["positions.json"] = positions
        };
    }

    private string ExecuteTransactionUnlocked(Dictionary<string, JsonObject> documents, IEnumerable<string> files, string backupDescription, string eventTitle, string eventDescription, long sourceRevision, string? undoOf = null, string? seatId = null)
    {
        var transactionFiles = files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (transactionFiles.Length == 0 || transactionFiles.Any(file => !TransactionFiles.Contains(file))) throw new InvalidDataException("Conjunto de ficheros de transacción inválido.");
        if (sourceRevision != CurrentRevisionUnlocked()) throw new InvalidOperationException("La revisión de datos cambió antes de confirmar la operación.");
        var destinationRevision = checked(sourceRevision + 1);
        var transactionId = Guid.NewGuid().ToString("N");
        string? backupId = null;
        Dictionary<string, string>? temporaries = null;
        try
        {
            backupId = CreateBackupUnlocked(transactionFiles, backupDescription);
            _logger.Info("transaction.backup", seatId: seatId, sourceRevision: sourceRevision, destinationRevision: destinationRevision, backupId: backupId, transactionId: transactionId, files: transactionFiles, backupOutcome: "created", bridgeAction: BridgeAction.Value);
            var events = documents.TryGetValue("events.json", out var eventDocument) ? eventDocument : ReadOptional("events.json") ?? New("events");
            AddEvent(events, eventTitle, eventDescription, backupId, undoOf);
            documents["events.json"] = events;
            transactionFiles = transactionFiles.Union(["events.json"], StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var file in transactionFiles)
            {
                if (!documents.TryGetValue(file, out var document)) throw new InvalidDataException($"Falta el documento transaccional {file}.");
                StampRevision(document, destinationRevision);
            }

            temporaries = transactionFiles.ToDictionary(file => file, file => DataPath(file) + "." + transactionId + ".tmp", StringComparer.OrdinalIgnoreCase);
            foreach (var file in transactionFiles) File.WriteAllText(temporaries[file], documents[file].ToJsonString(JsonOptions));
            var pending = new JsonObject
            {
                ["schemaVersion"] = "1.0",
                ["transactionId"] = transactionId,
                ["backupId"] = backupId,
                ["sourceRevision"] = sourceRevision,
                ["destinationRevision"] = destinationRevision,
                ["files"] = new JsonArray(transactionFiles.Select(file => JsonValue.Create(file)).ToArray()),
                ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"),
                ["createdBy"] = Environment.UserName
            };
            WriteAtomic(PendingPath, pending);
            foreach (var file in transactionFiles) File.Move(temporaries[file], DataPath(file), true);
            WriteStateUnlocked(destinationRevision);
            File.Delete(PendingPath);
            _logger.Info("transaction.commit", seatId: seatId, sourceRevision: sourceRevision, destinationRevision: destinationRevision, backupId: backupId, transactionId: transactionId, files: transactionFiles, backupOutcome: "committed", bridgeAction: BridgeAction.Value);
            return backupId;
        }
        catch (Exception exception)
        {
            _logger.Error("transaction.failed", exception, seatId: seatId, sourceRevision: sourceRevision, destinationRevision: destinationRevision, backupId: backupId, transactionId: transactionId, files: transactionFiles, backupOutcome: backupId is null ? "not-created" : "created", bridgeAction: BridgeAction.Value);
            throw;
        }
        finally
        {
            if (temporaries is not null) foreach (var temporary in temporaries.Values) if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private string CreateBackupUnlocked(IEnumerable<string> files, string description)
    {
        var selected = OperationalBackupFiles;
        var transactionFiles = files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (transactionFiles.Any(file => !TransactionFiles.Contains(file))) throw new InvalidDataException("Conjunto de ficheros de backup inválido.");
        var id = DateTime.Now.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N")[..6];
        Directory.CreateDirectory(BackupsRoot);
        var archivePath = Path.Combine(BackupsRoot, id + ".zip");
        var createdAtUtc = DateTimeOffset.UtcNow;
        var manifest = new JsonObject
        {
            ["id"] = id,
            ["title"] = "Backup",
            ["description"] = description,
            ["files"] = new JsonArray(selected.Select(file => JsonValue.Create(file)).ToArray()),
            ["transactionFiles"] = new JsonArray(transactionFiles.Select(file => JsonValue.Create(file)).ToArray()),
            ["sourceRevision"] = CurrentRevisionUnlocked(),
            ["createdAt"] = createdAtUtc.ToString("O"),
            ["createdAtUtc"] = createdAtUtc.ToString("O"),
            ["createdBy"] = Environment.UserName
        };
        var temporaryPath = archivePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
            {
                foreach (var file in selected)
                {
                    var source = DataPath(file);
                    if (File.Exists(source)) archive.CreateEntryFromFile(source, file, CompressionLevel.Optimal);
                    else if (string.Equals(file, ManagedAreas.FileName, StringComparison.OrdinalIgnoreCase))
                    {
                        var emptyManagedAreas = ManagedAreas.EmptyDocument();
                        StampRevision(emptyManagedAreas, CurrentRevisionUnlocked());
                        WriteZipJson(archive, file, emptyManagedAreas);
                    }
                }
                var state = StatePath;
                if (File.Exists(state)) archive.CreateEntryFromFile(state, "state.origin.json", CompressionLevel.Optimal);
                WriteZipJson(archive, "manifest.json", manifest);
            }
            using (var validation = ZipFile.OpenRead(temporaryPath))
                if (validation.GetEntry("manifest.json") is null) throw new InvalidDataException("El backup temporal no contiene manifiesto.");
            File.Move(temporaryPath, archivePath);
            return id;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private void RecoverPendingUnlocked()
    {
        if (!File.Exists(PendingPath)) return;
        JsonObject? pending = null;
        try { pending = JsonNode.Parse(File.ReadAllText(PendingPath))?.AsObject(); }
        catch (JsonException) { }
        var backupId = pending is null ? "desconocido" : Text(pending["backupId"]);
        var transactionId = pending is null ? null : Text(pending["transactionId"]);
        if (_config.ReadOnly)
        {
            _logger.Info("recovery.pending", backupId: backupId, transactionId: transactionId, backupOutcome: "read-only-blocked");
            throw new InvalidOperationException($"Hay una recuperación pendiente del backup {backupId}. Un usuario con permisos de escritura debe abrir la aplicación para completarla o restaurarla antes de continuar.");
        }
        if (pending is null || !TryPending(pending, out var files, out var source, out var destination, out backupId))
        {
            _logger.Info("recovery.invalid", backupId: backupId, transactionId: transactionId, backupOutcome: "manual-intervention-required");
            throw new InvalidOperationException($"No se pudo recuperar la transacción pendiente. Backup requerido: {backupId}. Un operador debe comprobar la copia de seguridad y restaurar una copia válida antes de abrir los datos.");
        }
        var folder = BackupPath(backupId);
        if (!BackupExists(folder) || !BackupContainsFiles(folder, files))
        {
            _logger.Info("recovery.backup-missing", sourceRevision: source, destinationRevision: destination, backupId: backupId, transactionId: transactionId, files: files, backupOutcome: "manual-intervention-required");
            throw new InvalidOperationException($"No se pudo recuperar la transacción pendiente. Backup requerido: {backupId}. Un operador debe comprobar la copia de seguridad y restaurar una copia válida antes de abrir los datos.");
        }

        var revisions = files.Select(file => ReadRevision(DataPath(file))).ToArray();
        var stateRevision = CurrentRevisionUnlocked();
        if (revisions.All(revision => revision == destination))
        {
            WriteStateUnlocked(destination);
            CleanupPending(files, Text(pending["transactionId"]));
            _logger.Info("recovery.confirmed", sourceRevision: source, destinationRevision: destination, backupId: backupId, transactionId: transactionId, files: files, backupOutcome: "confirmed");
            return;
        }
        if (revisions.All(revision => revision != destination) && stateRevision == source)
        {
            CleanupPending(files, Text(pending["transactionId"]));
            _logger.Info("recovery.discarded", sourceRevision: source, destinationRevision: destination, backupId: backupId, transactionId: transactionId, files: files, backupOutcome: "discarded");
            return;
        }

        var documents = LoadBackupDocuments(folder, files);
        foreach (var document in documents.Values) StampRevision(document, destination);
        var eventDocument = documents.TryGetValue("events.json", out var backedEvents) ? backedEvents : ReadOptional("events.json") ?? New("events");
        AddEvent(eventDocument, "Recuperación revertida", backupId, backupId, null, "reverted", source, destination);
        StampRevision(eventDocument, destination);
        documents["events.json"] = eventDocument;
        foreach (var (file, document) in documents) WriteAtomic(DataPath(file), document);
        if (Directory.Exists(folder))
        {
            var manifestPath = Path.Combine(folder, "manifest.json");
            var manifest = ReadJson(manifestPath) ?? new JsonObject { ["id"] = backupId };
            manifest["recovery"] = "reverted";
            manifest["sourceRevision"] = source;
            manifest["destinationRevision"] = destination;
            WriteAtomic(manifestPath, manifest);
        }
        WriteStateUnlocked(destination);
        CleanupPending(files, Text(pending["transactionId"]));
        _logger.Info("recovery.reverted", sourceRevision: source, destinationRevision: destination, backupId: backupId, transactionId: transactionId, files: files, backupOutcome: "reverted");
    }

    private void CleanupPending(IEnumerable<string> files, string transactionId)
    {
        foreach (var file in files)
        {
            var temporary = DataPath(file) + "." + transactionId + ".tmp";
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        if (File.Exists(PendingPath)) File.Delete(PendingPath);
    }

    private static bool TryPending(JsonObject pending, out string[] files, out long source, out long destination, out string backupId)
    {
        files = pending["files"]?.AsArray().Select(Text).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        source = 0;
        destination = 0;
        backupId = Text(pending["backupId"]);
        var valid = Guid.TryParseExact(Text(pending["transactionId"]), "N", out _) && IsBackupId(backupId) && TryRevision(pending["sourceRevision"], out source) && TryRevision(pending["destinationRevision"], out destination) && destination == source + 1 && files.Length > 0 && files.All(TransactionFiles.Contains);
        return valid;
    }

    private IEnumerable<(string Id, string Container, JsonObject Manifest, bool Legacy)> BackupContainersUnlocked()
    {
        if (!Directory.Exists(BackupsRoot)) return [];
        var containers = new List<(string Id, string Container, JsonObject Manifest, bool Legacy)>();
        foreach (var container in Directory.GetDirectories(BackupsRoot).Concat(Directory.GetFiles(BackupsRoot, "*.zip")))
        {
            try
            {
                var manifest = ReadBackupManifest(container);
                if (manifest is null) continue;
                containers.Add((Text(manifest["id"]), container, manifest, Directory.Exists(container) && manifest["files"] is not JsonArray));
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.Error("backup.manifest.invalid", exception, result: Path.GetFileName(container));
            }
        }
        return containers
            .OrderByDescending(item => BackupCreatedAtUtc(item.Manifest) ?? DateTimeOffset.MinValue)
            .ThenByDescending(item => item.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private JsonObject RetentionReportDocument(BackupRetentionReportData report)
    {
        JsonObject Entry(BackupRetentionReportEntry item) => new()
        {
            ["id"] = item.Id,
            ["createdAtUtc"] = item.CreatedAtUtc.ToString("O"),
            ["sizeBytes"] = item.SizeBytes,
            ["legacy"] = item.IsLegacy,
            ["undoProtected"] = item.IsProtected,
            ["retain"] = item.Retain,
            ["reason"] = item.Reason
        };

        return new JsonObject
        {
            ["generatedAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["retentionMode"] = "report",
            ["totalBackups"] = report.TotalBackups,
            ["totalBytes"] = report.TotalBytes,
            ["oldestCreatedAtUtc"] = report.OldestCreatedAtUtc?.ToString("O"),
            ["undoProtectedCount"] = report.UndoProtectedCount,
            ["reclaimableBytes"] = report.ReclaimableBytes,
            ["backups"] = new JsonArray(report.Backups.Select(Entry).ToArray()),
            ["candidates"] = new JsonArray(report.Backups.Where(item => !item.Retain).Select(Entry).ToArray())
        };
    }

    private void WriteRetentionReport(JsonObject document, int totalBackups, long reclaimableBytes)
    {
        try
        {
            Directory.CreateDirectory(LogsRoot);
            var path = Path.Combine(LogsRoot, $"backup-retention-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffffffZ}.json");
            document["reportPath"] = path;
            File.WriteAllText(path, document.ToJsonString(JsonOptions));
            _logger.Info("backup.retention.report", count: totalBackups, backupOutcome: "report", reportPath: path);
        }
        catch (Exception exception)
        {
            document.Remove("reportPath");
            _logger.Error("backup.retention.report.failed", exception, count: totalBackups, backupOutcome: "report");
        }
    }

    private void WriteIntegrityReport(JsonObject document)
    {
        try
        {
            Directory.CreateDirectory(LogsRoot);
            var path = Path.Combine(LogsRoot, $"integrity-report-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffffffZ}.json");
            document["reportPath"] = path;
            File.WriteAllText(path, document.ToJsonString(JsonOptions));
            var count = document["counts"]?.AsObject()?.Select(item => item.Value?.GetValue<int>() ?? 0).Sum() ?? 0;
            _logger.Info("integrity.report", count: count, currentRevision: CurrentRevisionUnlocked(), reportPath: path);
        }
        catch (Exception exception)
        {
            document.Remove("reportPath");
            _logger.Error("integrity.report.failed", exception);
        }
    }

    private string[] BackupFiles(string container) => BackupManifestFiles(container, "files", OperationalBackupFiles);

    private string[] BackupTransactionFiles(string container) => BackupManifestFiles(container, "transactionFiles", TransactionFiles);

    private string[] BackupManifestFiles(string container, string property, IEnumerable<string> allowed)
    {
        var manifest = ReadBackupManifest(container);
        var files = manifest?[property]?.AsArray().Select(Text).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (files is null || files.Length == 0) files = ["maps.json", "assignments.json", "positions.json"];
        if (files.Any(file => !allowed.Contains(file, StringComparer.OrdinalIgnoreCase))) throw new InvalidDataException("El backup contiene ficheros no compatibles.");
        return files;
    }

    // B6 usará createdAtUtc para calcular la antigüedad. Los manifiestos heredados
    // sin ese campo se interpretan como hora local para mantener compatibilidad.
    internal static DateTimeOffset? BackupCreatedAtUtc(JsonObject manifest)
    {
        if (DateTimeOffset.TryParse(Text(manifest["createdAtUtc"]), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc)) return utc;
        if (!DateTime.TryParse(Text(manifest["createdAt"]), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var legacy)) return null;
        return new DateTimeOffset(DateTime.SpecifyKind(legacy, DateTimeKind.Local)).ToUniversalTime();
    }

    private Dictionary<string, JsonObject> LoadBackupDocuments(string container, IEnumerable<string> files)
    {
        var documents = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(container))
        {
            using var archive = ZipFile.OpenRead(container);
            foreach (var file in files)
            {
                var entry = archive.GetEntry(file) ?? throw new FileNotFoundException($"Falta {file} en el backup.");
                using var stream = entry.Open();
                documents[file] = JsonNode.Parse(stream)?.AsObject() ?? throw new InvalidDataException($"{file} no es válido.");
            }
            return documents;
        }
        foreach (var file in files)
        {
            var document = ReadJson(Path.Combine(container, file));
            if (document is null) throw new FileNotFoundException($"Falta {file} en el backup.");
            documents[file] = document;
        }
        return documents;
    }

    private T WithLock<T>(string action, Func<T> operation)
    {
        try
        {
            using var heldLock = AcquireLock();
            EnsureStateUnlocked();
            RecoverPendingUnlocked();
            return operation();
        }
        catch (Exception exception)
        {
            _logger.Error(action, exception);
            throw;
        }
    }

    private void LogLifecycle(string action)
    {
        _logger.Info(action, currentRevision: CurrentRevisionForAudit(), applicationBuild: GetType().Assembly.ManifestModule.ModuleVersionId.ToString("D"));
    }

    private long? CurrentRevisionForAudit()
    {
        try { return WithLock("audit.revision", CurrentRevisionUnlocked); }
        catch { return null; }
    }

    private FileStream AcquireLock()
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        var delay = 100;
        var waits = 0;
        while (true)
        {
            try { return new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (UnauthorizedAccessException ex)
            {
                _logger.Error("lock.open.failed", ex);
                throw new InvalidOperationException("No se pudo abrir el bloqueo de datos. Comprueba los permisos de la carpeta compartida.", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.Error("lock.open.failed", ex);
                throw new InvalidOperationException("No se pudo abrir el bloqueo de datos. Comprueba los permisos de la carpeta compartida.", ex);
            }
            catch (IOException ex)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    _logger.Error("lock.timeout", ex, count: waits);
                    throw new InvalidOperationException("No se pudo adquirir el bloqueo de datos tras 10 segundos. Otro usuario puede estar guardando cambios; recarga e inténtalo de nuevo.", ex);
                }
                waits++;
                _logger.Info("lock.wait", count: waits);
                Thread.Sleep(delay);
                delay = Math.Min(delay * 2, 1000);
            }
        }
    }

    private void EnsureStateUnlocked()
    {
        if (File.Exists(StatePath)) return;
        var inferred = ReadOptional("assignments.json") is { } assignments && TryRevision(assignments["version"], out var version) ? version : 0;
        WriteStateUnlocked(inferred);
        _logger.Info("state.bootstrap", sourceRevision: inferred, destinationRevision: inferred);
    }

    private long CurrentRevisionUnlocked()
    {
        var state = ReadJson(StatePath) ?? throw new InvalidDataException("state.json no es válido.");
        if (!TryRevision(state["revision"], out var revision)) throw new InvalidDataException("state.json no contiene una revisión válida.");
        return revision;
    }

    private void WriteStateUnlocked(long revision)
    {
        if (revision < 0) throw new InvalidDataException("Revisión de estado inválida.");
        WriteAtomic(StatePath, new JsonObject { ["schemaVersion"] = "1.0", ["revision"] = revision, ["updatedAt"] = DateTimeOffset.UtcNow.ToString("O"), ["updatedBy"] = Environment.UserName });
    }

    private static void MigrateLegacyScenario(JsonObject scenario, JsonObject real)
    {
        scenario["base"] = real.DeepClone();
        var draft = (JsonObject)real.DeepClone();
        foreach (var change in scenario["changes"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            if (Text(change["type"]) != "seat.created") continue;
            var payload = change["payload"]?.AsObject();
            var map = payload is null ? null : draft["maps"]?["maps"]?.AsArray().OfType<JsonObject>().FirstOrDefault(item => Text(item["id"]) == Text(payload["mapId"]));
            var seat = payload?["seat"]?.AsObject();
            if (map is not null && seat is not null) (map["seats"]?.AsArray() ?? new JsonArray()).Add(seat.DeepClone());
        }
        scenario["draft"] = draft;
        scenario["undo"] = new JsonArray();
        scenario["migratedAt"] = DateTimeOffset.UtcNow.ToString("O");
    }

    private JsonObject Package(JsonObject state, JsonObject scenarios, JsonObject? active)
    {
        var people = ReadRequired("people.json");
        var devices = ReadRequired("devices.json");
        var locations = ReadRequired("locations.json");
        var maps = state["maps"]?.DeepClone()?.AsObject() ?? New("maps");
        var assignments = state["assignments"]?.DeepClone()?.AsObject() ?? New("assignments");
        var bySeat = assignments["assignments"]?.AsArray().OfType<JsonObject>().GroupBy(item => Text(item["workstationId"]), StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal) ?? [];
        foreach (var seat in maps["maps"]?.AsArray().OfType<JsonObject>().SelectMany(map => map["seats"]?.AsArray().OfType<JsonObject>() ?? []) ?? [])
        {
            var assignment = bySeat.GetValueOrDefault(Text(seat["id"]));
            var effectiveState = SeatStates.DeriveEffectiveWorkspaceState(seat, assignment);
            seat["state"] = SeatStates.WireName(effectiveState.State);
            seat["stateMode"] = SeatStates.WireName(effectiveState.Mode);
            seat["completeness"] = SeatStates.WireCompleteness(SeatStates.Completeness(assignment, seat));
            seat["displayLocation"] = SpatialLocation.FromSeat(seat);
        }

        return new JsonObject
        {
            ["maps"] = maps,
            ["assignments"] = assignments,
            ["grid"] = new JsonObject { ["columns"] = GridColumns, ["rows"] = GridRows },
            ["people"] = people,
            ["devices"] = devices,
            ["locations"] = locations,
            ["managedAreas"] = ManagedAreas.Normalize(ReadOptional(ManagedAreas.FileName), maps),
            ["version"] = state["version"]?.DeepClone(),
            ["scenarios"] = new JsonArray(scenarios["scenarios"]?.AsArray().OfType<JsonObject>().Select(s => new JsonObject { ["id"] = s["id"]?.DeepClone(), ["name"] = s["name"]?.DeepClone(), ["createdAt"] = s["createdAt"]?.DeepClone(), ["createdBy"] = s["createdBy"]?.DeepClone() }).ToArray() ?? []),
            ["activeScenario"] = active is null ? null : new JsonObject { ["id"] = active["id"]?.DeepClone(), ["name"] = active["name"]?.DeepClone(), ["undoCount"] = active["undo"]?.AsArray().Count ?? 0, ["isPrimary"] = active["isPrimary"]?.DeepClone() ?? false }
        };
    }

    private string[] ValidateAssignment(JsonObject state, SaveAssignmentRequest request)
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

    private static string ValueOrUnassigned(JsonNode? value) => Text(value) is { Length: > 0 } text ? text : "Sin asignar";

    private static JsonArray WarningArray(IEnumerable<string> warnings) => new(warnings.Select(value => JsonValue.Create(value)).ToArray());

    private void SetAssignment(JsonObject state, SaveAssignmentRequest request)
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

    private void DeleteAssignment(JsonObject state, DeleteAssignmentRequest request)
    {
        var list = state["assignments"]?["assignments"]?.AsArray() ?? new JsonArray();
        Remove(list, value => Text(value?["workstationId"]) == request.WorkstationId!); UpdateSeatName(state, request.WorkstationId!, request.SeatName, request.Has("seatName"));
        state["assignments"] ??= New("assignments"); state["assignments"]!["assignments"] = list;
    }

    private static void SetReceived(JsonObject item, string name, string? value, bool received) { if (received) item[name] = value; }
    private static void UpdateSeatName(JsonObject state, string workstationId, string? seatName, bool received)
    {
        if (!received) return;
        var editableSeat = state["maps"]?["maps"]?.AsArray().OfType<JsonObject>().SelectMany(map => map["seats"]?.AsArray().OfType<JsonObject>() ?? []).FirstOrDefault(item => Text(item["id"]) == workstationId);
        if (editableSeat is not null) editableSeat["name"] = seatName ?? "";
    }
    private static void SetPosition(JsonObject state, SavePositionRequest request) { var seat = Seat(state, request.MapId!, request.SeatId!); var x = Coordinate(request.X); var y = Coordinate(request.Y); seat["x"] = x; seat["y"] = y; seat["gridCell"] = Cell(x, y); seat["updatedAt"] = DateTimeOffset.UtcNow.ToString("O"); seat["updatedBy"] = Environment.UserName; }
    private static string AddSeat(JsonObject state, CreateSeatRequest request)
    {
        var map = Map(state, request.MapId!); var seats = map["seats"]?.AsArray() ?? new JsonArray(); var x = Coordinate(request.X); var y = Coordinate(request.Y);
        var id = "custom-" + Guid.NewGuid().ToString("N"); var cell = Cell(x, y); var name = $"{MapPrefix(Text(map["id"]))}-{cell}";
        seats.Add(new JsonObject { ["id"] = id, ["name"] = name, ["type"] = "free", ["x"] = x, ["y"] = y, ["gridCell"] = cell, ["updatedBy"] = Environment.UserName }); map["seats"] = seats;
        return id;
    }
    private static void RemoveSeat(JsonObject state, DeleteSeatRequest request) { var map = Map(state, request.MapId!); Remove(map["seats"]?.AsArray() ?? new JsonArray(), s => Text(s?["id"]) == request.SeatId!); Remove(state["assignments"]?["assignments"]?.AsArray() ?? new JsonArray(), s => Text(s?["workstationId"]) == request.SeatId!); }

    private JsonArray Diff(JsonObject before, JsonObject after, IReadOnlyList<ScenarioOperation>? operations = null) => new(ScenarioDiffEngine.Compare(before, after, operations: operations).Changes.Select(ScenarioDiffJson).ToArray());

    private static ScenarioComparison CompareScenario(JsonObject before, JsonObject after, IReadOnlyList<ScenarioOperation>? operations = null)
    {
        var baseValidation = ValidationEngine.OperationalResults(ValidationEngine.Run(before["maps"]?.AsObject() ?? throw new InvalidDataException("Faltan planos."), before["assignments"]?.AsObject() ?? throw new InvalidDataException("Faltan asignaciones.")));
        var draftValidation = ValidationEngine.OperationalResults(ValidationEngine.Run(after["maps"]?.AsObject() ?? throw new InvalidDataException("Faltan planos."), after["assignments"]?.AsObject() ?? throw new InvalidDataException("Faltan asignaciones.")));
        return ScenarioDiffEngine.Compare(before, after, baseValidation, draftValidation, operations);
    }

    private static JsonObject ScenarioComparisonJson(ScenarioComparison comparison) => new()
    {
        ["changes"] = new JsonArray(comparison.Changes.Select(ScenarioDiffJson).ToArray()),
        ["impactSummary"] = new JsonObject
        {
            ["total"] = comparison.ImpactSummary.Total, ["added"] = comparison.ImpactSummary.Added,
            ["removed"] = comparison.ImpactSummary.Removed, ["moved"] = comparison.ImpactSummary.Moved,
            ["modified"] = comparison.ImpactSummary.Modified, ["assignments"] = comparison.ImpactSummary.Assignments,
            ["workspaces"] = comparison.ImpactSummary.Workspaces, ["changedFields"] = comparison.ImpactSummary.ChangedFields,
            ["byMap"] = new JsonObject(comparison.ImpactSummary.ByMap.ToDictionary(pair => pair.Key, pair => (JsonNode?)pair.Value))
        },
        ["validationImpact"] = new JsonObject
        {
            ["introduced"] = new JsonArray(comparison.ValidationImpact.Introduced.Select(ValidationJson).ToArray()),
            ["resolved"] = new JsonArray(comparison.ValidationImpact.Resolved.Select(ValidationJson).ToArray()),
            ["persistent"] = new JsonArray(comparison.ValidationImpact.Persistent.Select(ValidationJson).ToArray())
        }
    };

    private static JsonObject ScenarioDiffJson(ScenarioDiffChange change) => new()
    {
        ["id"] = change.Id, ["kind"] = change.Kind.ToString().ToUpperInvariant(), ["type"] = change.Operation?.Type ?? change.Kind.ToString().ToUpperInvariant(),
        ["operationId"] = change.Operation?.Id, ["atomic"] = change.Operation?.Atomic ?? false,
        ["entityType"] = change.EntityType, ["entityId"] = change.EntityId, ["seatId"] = change.EntityId,
        ["mapId"] = change.MapId, ["mapName"] = change.MapName, ["fromCell"] = change.FromCell, ["toCell"] = change.ToCell,
        ["before"] = change.Before?.DeepClone(), ["after"] = change.After?.DeepClone(),
        ["changedFields"] = new JsonArray(change.ChangedFields.Select(field => new JsonObject { ["field"] = field.Field, ["before"] = field.Before?.DeepClone(), ["after"] = field.After?.DeepClone() }).ToArray())
    };

    private static JsonObject MovementOperationJson(string scenarioId, MovementProposal proposal, bool legacySource = false) => new()
    {
        ["id"] = MovementOperationId(scenarioId, proposal.Source, proposal.Destination),
        ["type"] = "movement",
        ["atomic"] = true,
        ["members"] = legacySource
            ? new JsonArray($"seat|{proposal.Source.MapId}|{proposal.Source.WorkspaceId}", $"assignment|{proposal.Destination.WorkspaceId}")
            : new JsonArray($"assignment|{proposal.Source.WorkspaceId}", $"assignment|{proposal.Destination.WorkspaceId}")
    };

    private static JsonObject LegacyDestinationAssignment(MovementProposal proposal, string personId, string? deviceId)
    {
        var assignment = new JsonObject
        {
            ["workstationId"] = proposal.Destination.WorkspaceId,
            ["personId"] = personId,
            ["roseta"] = proposal.Destination.Roseta,
            ["updatedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["updatedBy"] = Environment.UserName
        };
        if (deviceId is not null) assignment["deviceId"] = deviceId;
        return assignment;
    }

    private static string LegacyPersonId(JsonObject sourceSeat, JsonObject people)
    {
        var personId = Text(sourceSeat["personId"]);
        var matches = people["people"]?.AsArray().OfType<JsonObject>()
            .Where(person => string.Equals(Text(person["id"]), personId, StringComparison.Ordinal))
            .Select(person => Text(person["id"]))
            .ToArray() ?? [];
        return matches.Length == 1 ? matches[0] : throw new InvalidDataException("La persona heredada del puesto de origen no existe o no es única.");
    }

    private static string? LegacyDeviceId(JsonObject sourceSeat, JsonObject devices)
    {
        var deviceName = Text(sourceSeat["deviceName"]);
        if (deviceName.Length == 0) return null;
        var matches = devices["devices"]?.AsArray().OfType<JsonObject>()
            .Where(device => string.Equals(Text(device["name"]), deviceName, StringComparison.Ordinal) && Text(device["id"]).Length > 0)
            .Select(device => Text(device["id"]))
            .ToArray() ?? [];
        return matches.Length == 1 ? matches[0] : throw new InvalidDataException("El dispositivo heredado del puesto de origen no existe o no es único.");
    }

    private static string MovementOperationId(string scenarioId, MovementEndpoint source, MovementEndpoint destination) =>
        $"movement|{scenarioId}|{source.MapId}|{source.WorkspaceId}|{destination.MapId}|{destination.WorkspaceId}";

    private static IReadOnlyList<ScenarioOperation> ScenarioOperations(JsonObject scenario) => scenario["operations"]?.AsArray().OfType<JsonObject>().Select(operation =>
    {
        var id = Text(operation["id"]);
        var type = Text(operation["type"]);
        var members = operation["members"]?.AsArray().Select(Text).ToArray() ?? [];
        if (id.Length == 0 || type.Length == 0 || members.Length == 0 || members.Any(member => member.Length == 0) || members.Distinct(StringComparer.Ordinal).Count() != members.Length)
            throw new InvalidDataException("Escenario corrupto.");
        return new ScenarioOperation(id, type, operation["atomic"]?.GetValue<bool>() ?? false, members);
    }).ToArray() ?? [];

    private static Dictionary<string, JsonObject> Seats(JsonObject state) => state["maps"]?["maps"]?.AsArray().OfType<JsonObject>().SelectMany(map => map["seats"]?.AsArray().OfType<JsonObject>().Select(seat => { var copy = (JsonObject)seat.DeepClone(); copy["mapId"] = map["id"]?.DeepClone(); copy["mapName"] = map["name"]?.DeepClone(); return copy; }) ?? []).ToDictionary(seat => Text(seat["mapId"]) + "|" + Text(seat["id"])) ?? [];
    private void ApplyChange(JsonObject real, JsonObject change, JsonObject draft)
    {
        var id = Text(change["id"]); if (id.StartsWith("assignment|")) { var seat = id[11..]; var draftItem = Index(draft["assignments"]?["assignments"]?.AsArray(), "workstationId").GetValueOrDefault(seat); var realList = real["assignments"]?["assignments"]?.AsArray() ?? new JsonArray(); Remove(realList, x => Text(x?["workstationId"]) == seat); if (draftItem is not null) realList.Add(draftItem.DeepClone()); real["assignments"]!["assignments"] = realList; return; }
        var key = id[5..]; var draftSeat = Seats(draft).GetValueOrDefault(key); var parts = key.Split('|'); var map = Map(real, parts[0]); var seats = map["seats"]?.AsArray() ?? new JsonArray(); Remove(seats, x => Text(x?["id"]) == parts[1]); if (draftSeat is not null) { draftSeat.Remove("mapId"); draftSeat.Remove("mapName"); seats.Add(draftSeat); } map["seats"] = seats;
    }

    private static bool SameMeaningful(JsonNode? before, JsonNode? after) => JsonNode.DeepEquals(Meaningful(before), Meaningful(after));
    private static JsonNode? Meaningful(JsonNode? source)
    {
        if (source is null) return null;
        var copy = source.DeepClone();
        if (copy is JsonObject item) { var cell = GridCell(item); foreach (var key in new[] { "updatedAt", "updatedBy", "mapId", "mapName", "x", "y" }) item.Remove(key); if (cell.Length > 0) item["gridCell"] = cell; }
        return copy;
    }
    private static string GridCell(JsonNode? item) => item is JsonObject value && Coordinate(value["x"], out var x) && Coordinate(value["y"], out var y) ? Cell(x, y) : "";
    private static string Cell(double x, double y) { var column = Math.Clamp((int)Math.Floor(x * GridColumns), 0, GridColumns - 1); var row = Math.Clamp((int)Math.Floor(y * GridRows), 0, GridRows - 1); return $"{ColumnName(column)}-{row + 1:D2}"; }
    private static string MapPrefix(string mapId) => mapId.ToLowerInvariant() switch { "norte" => "NOR", "nivel3" => "N3", "sur" => "SUR", "id" => "ID", "qc" => "QC", _ => mapId.ToUpperInvariant() };
    private static string ColumnName(int column) { var result = ""; for (column++; column > 0; column = (column - 1) / 26) result = (char)('A' + (column - 1) % 26) + result; return result; }
    private static bool Coordinate(JsonNode? node, out double value) { value = 0; return node is JsonValue json && json.TryGetValue<double>(out value); }

    private JsonObject ScenariosUnlocked() => ReadOptional("scenarios.json") ?? New("scenarios");
    private JsonObject FindScenarioUnlocked(string id) => ScenariosUnlocked()["scenarios"]?.AsArray().OfType<JsonObject>().FirstOrDefault(s => Text(s["id"]) == id) ?? throw new InvalidDataException("Escenario inexistente.");
    private string ScenariosPath => DataPath("scenarios.json");
    private string BackupPath(string id)
    {
        if (!IsBackupId(id)) throw new InvalidDataException("Formato de backup inválido.");
        var root = Path.GetFullPath(BackupsRoot); var candidate = Path.GetFullPath(Path.Combine(root, id)); var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ruta de backup inválida.");
        var archive = candidate + ".zip";
        return File.Exists(archive) ? archive : candidate;
    }

    private static bool BackupExists(string container) => Directory.Exists(container) || File.Exists(container);

    private static bool BackupContainsFiles(string container, IEnumerable<string> files)
    {
        if (File.Exists(container))
        {
            using var archive = ZipFile.OpenRead(container);
            return files.All(file => archive.GetEntry(file) is not null);
        }
        return files.All(file => File.Exists(Path.Combine(container, file)));
    }

    private static JsonObject? ReadBackupManifest(string container)
    {
        if (File.Exists(container))
        {
            using var archive = ZipFile.OpenRead(container);
            var entry = archive.GetEntry("manifest.json");
            if (entry is null) return null;
            using var stream = entry.Open();
            return JsonNode.Parse(stream)?.AsObject();
        }
        return ReadJson(Path.Combine(container, "manifest.json"));
    }

    private static void WriteZipJson(ZipArchive archive, string name, JsonObject value)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(value.ToJsonString(JsonOptions));
    }

    private sealed record SaveAssignmentRequest(string? WorkstationId, string? PersonId, string? DeviceId, string? LocationId, string? Roseta, string? Status, string? Notes, string? SeatName, string? ScenarioId, IReadOnlySet<string>? ReceivedFields = null)
    {
        public bool Has(string field) => ReceivedFields?.Contains(field) == true;
        public static SaveAssignmentRequest From(JsonObject payload) { var request = Bind<SaveAssignmentRequest>(payload, "saveAssignment", new[] { "workstationId", "personId", "deviceId", "locationId", "roseta", "status", "notes", "seatName", "scenarioId" }, out var fields); return request with { WorkstationId = Required(request.WorkstationId, "Puesto inválido."), ScenarioId = NormalizeScenarioId(request.ScenarioId), ReceivedFields = fields }; }
    }
    private sealed record DeleteAssignmentRequest(string? WorkstationId, string? SeatName, string? ScenarioId, IReadOnlySet<string>? ReceivedFields = null)
    {
        public bool Has(string field) => ReceivedFields?.Contains(field) == true;
        public static DeleteAssignmentRequest From(JsonObject payload) { var request = Bind<DeleteAssignmentRequest>(payload, "deleteAssignment", new[] { "workstationId", "seatName", "scenarioId" }, out var fields); return request with { WorkstationId = Required(request.WorkstationId, "Puesto inválido."), ScenarioId = NormalizeScenarioId(request.ScenarioId), ReceivedFields = fields }; }
    }
    private sealed record BulkAssignmentRequest(List<string>? WorkstationIds, string? Status, string? ScenarioId)
    {
        public static BulkAssignmentRequest From(JsonObject payload)
        {
            var request = Bind<BulkAssignmentRequest>(payload, "bulkUpdateAssignments", new[] { "workstationIds", "status", "scenarioId" }, out _);
            var ids = request.WorkstationIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? [];
            var status = request.Status?.Trim().ToLowerInvariant();
            if (status is not ("reserved" or "confirmed")) throw new InvalidDataException("El estado masivo debe ser reservado o automático.");
            return request with { WorkstationIds = ids, Status = status, ScenarioId = NormalizeScenarioId(request.ScenarioId) };
        }
    }

    private sealed record SavePositionRequest(string? MapId, string? SeatId, double? X, double? Y, string? ScenarioId)
    {
        public static SavePositionRequest From(JsonObject payload) { var request = Bind<SavePositionRequest>(payload, "savePosition", new[] { "mapId", "seatId", "x", "y", "scenarioId" }, out _); return request with { MapId = Required(request.MapId, "Plano inválido."), SeatId = Required(request.SeatId, "Puesto inválido."), ScenarioId = NormalizeScenarioId(request.ScenarioId) }; }
    }
    private sealed record CreateSeatRequest(string? MapId, double? X, double? Y, string? ScenarioId, string? TargetManagedAreaId)
    {
        public static CreateSeatRequest From(JsonObject payload) { var request = Bind<CreateSeatRequest>(payload, "createSeat", new[] { "mapId", "x", "y", "scenarioId", "targetManagedAreaId" }, out _); return request with { MapId = Required(request.MapId, "Plano inválido."), ScenarioId = NormalizeScenarioId(request.ScenarioId), TargetManagedAreaId = string.IsNullOrWhiteSpace(request.TargetManagedAreaId) ? null : request.TargetManagedAreaId.Trim() }; }
    }
    private sealed record DeleteSeatRequest(string? MapId, string? SeatId, string? ScenarioId)
    {
        public static DeleteSeatRequest From(JsonObject payload) { var request = Bind<DeleteSeatRequest>(payload, "deleteSeat", new[] { "mapId", "seatId", "scenarioId" }, out _); return request with { MapId = Required(request.MapId, "Plano inválido."), SeatId = Required(request.SeatId, "Puesto inválido."), ScenarioId = NormalizeScenarioId(request.ScenarioId) }; }
    }

    private static void ValidatePayloadFields(JsonObject payload, string action, IEnumerable<string> allowed)
    {
        var allowedFields = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, _) in payload)
            if (!allowedFields.Contains(name)) throw new InvalidDataException($"Campo no reconocido: '{name}' en {action}");
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

    private static string FirstText(JsonObject payload, params string[] keys) => keys.Select(key => Text(payload[key]).Trim()).FirstOrDefault(value => value.Length > 0) ?? "";
    private static string RequiredFirst(JsonObject payload, string error, params string[] keys) => FirstText(payload, keys) is { Length: > 0 } value ? value : throw new InvalidDataException(error);

    private static T Bind<T>(JsonObject payload, string action, IEnumerable<string> allowed, out HashSet<string> fields)
    {
        var allowedFields = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase); fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, _) in payload) { if (!allowedFields.Contains(name)) throw new InvalidDataException($"Campo no reconocido: '{name}' en {action}"); fields.Add(name); }
        return JsonSerializer.Deserialize<T>(payload.ToJsonString(), JsonOptions) ?? throw new InvalidDataException($"Payload inválido en {action}.");
    }

    private static void AddEvent(JsonObject document, string action, string description, string? backupId = null, string? undoOf = null, string? recovery = null, long? sourceRevision = null, long? destinationRevision = null)
    {
        var list = document["events"]?.AsArray() ?? new JsonArray();
        var item = new JsonObject { ["id"] = Guid.NewGuid().ToString("N"), ["title"] = action, ["description"] = description, ["backupId"] = backupId, ["undoOf"] = undoOf, ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"), ["createdBy"] = Environment.UserName };
        if (recovery is not null) { item["recovery"] = recovery; item["sourceRevision"] = sourceRevision; item["destinationRevision"] = destinationRevision; }
        list.Add(item); document["events"] = list;
    }
    private JsonObject ReadRequired(string name) => ReadOptional(name) ?? throw new FileNotFoundException($"Falta {name}.");
    private JsonObject? ReadOptional(string name) => ReadJson(DataPath(name));
    private static JsonObject? ReadJson(string path) => File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))?.AsObject() : null;
    private string DataPath(string name) => Path.Combine(_data, name);
    private string LockPath => DataPath(".lock");
    private string StatePath => DataPath("state.json");
    private string PendingPath => DataPath("commit.pending");
    private string BackupsRoot => Path.Combine(_root, _config.BackupFolder, "spatial-git");
    private string LogsRoot => Path.Combine(_root, _config.LogsFolder);
    private static JsonObject New(string array) => new() { ["schemaVersion"] = "1.0", ["version"] = 0, [array] = new JsonArray() };
    private static void WriteAtomic(string path, JsonObject value)
    {
        var temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(value.ToJsonString(JsonOptions));
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, path, true);
    }
    private static void Bump(JsonObject value) { value["version"] = (value["version"]?.GetValue<int>() ?? 0) + 1; value["updatedAt"] = DateTimeOffset.UtcNow.ToString("O"); value["updatedBy"] = Environment.UserName; }
    private static void StampRevision(JsonObject document, long revision) => document["stateRevision"] = revision;
    private static bool TryRevision(JsonNode? node, out long revision)
    {
        revision = 0;
        return node is JsonValue value && value.TryGetValue<long>(out revision) && revision >= 0;
    }
    private static long? ReadRevision(string path) { var document = ReadJson(path); return document is not null && TryRevision(document["stateRevision"], out var revision) ? revision : null; }
    private static bool IsBackupId(string id) => Regex.IsMatch(id, "^[0-9]{17}-[0-9a-fA-F]{6}$");
    private static Dictionary<string, JsonObject> Index(JsonArray? values, string key) => values?.OfType<JsonObject>().Where(v => Text(v[key]).Length > 0).ToDictionary(v => Text(v[key]), v => v) ?? [];
    private static JsonObject Map(JsonObject state, string id) => state["maps"]?["maps"]?.AsArray().OfType<JsonObject>().FirstOrDefault(m => Text(m["id"]) == id) ?? throw new InvalidDataException("Plano inexistente.");
    private static JsonObject Seat(JsonObject state, string mapId, string id) => Map(state, mapId)["seats"]?.AsArray().OfType<JsonObject>().FirstOrDefault(s => Text(s["id"]) == id) ?? throw new InvalidDataException("Puesto inexistente.");
    private static void Remove(JsonArray list, Func<JsonNode?, bool> predicate) { for (var i = list.Count - 1; i >= 0; i--) if (predicate(list[i])) list.RemoveAt(i); }
    private void EnsureWritable() { if (_config.ReadOnly) throw new InvalidOperationException("Modo solo lectura."); }
    private static string? NormalizeScenarioId(string? scenarioId) => string.IsNullOrWhiteSpace(scenarioId) ? null : scenarioId;
    private static string Required(string? value, string error) => !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidDataException(error);
    private static string Required(JsonObject payload, string key, string error) => Text(payload[key]) is { Length: > 0 } value ? value : throw new InvalidDataException(error);
    private static double Coordinate(double? value) => value is >= 0 and <= 1 ? value.Value : throw new InvalidDataException("Coordenada inválida.");
    private static string Text(JsonNode? value) => value?.ToString() ?? "";

    private sealed class BridgeActionScope(string? previous) : IDisposable
    {
        public void Dispose() => BridgeAction.Value = previous;
    }
}
