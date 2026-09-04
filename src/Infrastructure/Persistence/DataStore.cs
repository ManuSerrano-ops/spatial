using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

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
    private readonly BackupService _backups;
    private readonly TransactionCoordinator _transactions;
    private readonly AssignmentService _assignments;
    private readonly ManagedAreaService _managedAreas;
    private readonly ReportService _reports;

    private DataStore(AppConfig config, Func<JsonObject, string, XlsxExportResult>? xlsxWriter = null)
    {
        _config = config;
        _root = config.NetworkRoot;
        _data = Path.Combine(_root, config.DataFolder);
        _logger = new SafeLogger(Path.Combine(_root, config.LogsFolder), config.LogMaxFileSizeBytes, config.LogMaxHistoryFiles);
        _xlsxWriter = xlsxWriter ?? XlsxExporter.Write;
        _backups = CreateBackupService();
        _transactions = CreateTransactionCoordinator();
        _assignments = CreateAssignmentService();
        _managedAreas = CreateManagedAreaService();
        _reports = CreateReportService();
    }

    private BackupService CreateBackupService() => new(new(
        OperationalBackupFiles.ToHashSet(StringComparer.OrdinalIgnoreCase), TransactionFiles, BackupsRoot, StatePath, DataPath, CurrentRevisionUnlocked,
        (exception, result) => _logger.Error("backup.manifest.invalid", exception, result: result), LogsRoot, path => { Directory.CreateDirectory(path); }, File.WriteAllText,
        audit => _logger.Info(audit.Action, count: audit.Count, backupOutcome: audit.BackupOutcome, reportPath: audit.ReportPath),
        (audit, exception) => _logger.Error(audit.Action, exception, count: audit.Count, backupOutcome: audit.BackupOutcome, reportPath: audit.ReportPath)));

    private TransactionCoordinator CreateTransactionCoordinator() => new(new(
        TransactionFiles, _config.ReadOnly, DataPath, PendingPath, CurrentRevisionUnlocked, WriteStateUnlocked, ReadJson, WriteAtomic,
        File.WriteAllText, (source, destination) => File.Move(source, destination, true), File.Delete, File.Exists, Directory.Exists,
        _backups.Create, _backups.ResolvePath, _backups.Exists, _backups.ContainsFiles, _backups.LoadDocuments, BackupService.IsId,
        LogTransactionInfo, LogTransactionError));

    private AssignmentService CreateAssignmentService() => new(new(RealStateUnlocked, FindScenarioUnlocked, MutateScenarioUnlocked, CommitRealUnlocked));

    private ManagedAreaService CreateManagedAreaService() => new(new(
        () => ReadRequired("maps.json"), () => ReadOptional(ManagedAreas.FileName), RealDocuments, RealFiles,
        (documents, files, backupDescription, eventTitle, eventDescription, seatId) => _transactions.Execute(documents, files, backupDescription, eventTitle, eventDescription, CurrentRevisionUnlocked(), seatId: seatId)));

    private ReportService CreateReportService() => new(new(
        LoadUnlocked, FindScenarioUnlocked, ReadRequired, LogsRoot, path => { Directory.CreateDirectory(path); }, File.WriteAllText, CurrentRevisionUnlocked,
        audit => _logger.Info(audit.Action, scenarioId: audit.ScenarioId, count: audit.Count, durationMs: audit.DurationMs, details: audit.Details, currentRevision: audit.CurrentRevision, reportPath: audit.ReportPath),
        (action, exception) => _logger.Error(action, exception)));

    private void LogTransactionInfo(TransactionCoordinator.TransactionAudit audit) => _logger.Info(audit.Action, seatId: audit.SeatId, sourceRevision: audit.SourceRevision, destinationRevision: audit.DestinationRevision, backupId: audit.BackupId, transactionId: audit.TransactionId, files: audit.Files, backupOutcome: audit.BackupOutcome, bridgeAction: BridgeAction.Value);

    private void LogTransactionError(TransactionCoordinator.TransactionAudit audit, Exception exception) => _logger.Error(audit.Action, exception, seatId: audit.SeatId, sourceRevision: audit.SourceRevision, destinationRevision: audit.DestinationRevision, backupId: audit.BackupId, transactionId: audit.TransactionId, files: audit.Files, backupOutcome: audit.BackupOutcome, bridgeAction: BridgeAction.Value);

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

    public JsonObject RunValidation(string? scenarioId = null) => WithLock("validation.run", () => _reports.RunValidation(scenarioId));

    public JsonObject RunSpatialAnalytics(string? scenarioId = null) => WithLock("analytics.run", () => _reports.RunSpatialAnalytics(scenarioId));

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
            ["relatedProblems"] = new JsonArray(proposal.RelatedProblems.Select(ReportService.ValidationJson).ToArray()),
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
        var scenarioList = scenarios["scenarios"]?.AsArray() ?? new JsonArray(); scenarioList.Add(item); scenarios["scenarios"] = scenarioList; TransactionCoordinator.StampRevision(scenarios, CurrentRevisionUnlocked()); WriteAtomic(ScenariosPath, scenarios);
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
        TransactionCoordinator.StampRevision(scenarios, CurrentRevisionUnlocked());
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
        TransactionCoordinator.StampRevision(document, CurrentRevisionUnlocked());
        WriteAtomic(ScenariosPath, document);
        return new JsonObject { ["deleted"] = id };
    });

    public JsonObject SaveAssignment(JsonObject payload, bool delete) => WithLock(delete ? "assignment.delete" : "assignment.save", () =>
    {
        EnsureWritable();
        return _assignments.SaveAssignment(payload, delete);
    });

    public JsonObject BulkUpdateAssignments(JsonObject payload) => WithLock("workspace.bulk", () =>
    {
        EnsureWritable();
        return _assignments.BulkUpdateAssignments(payload);
    });

    public JsonObject CreateManagedArea(JsonObject payload) => WithLock("managed-area.create", () =>
    {
        EnsureWritable();
        return _managedAreas.Create(payload);
    });

    public JsonObject RenameManagedArea(JsonObject payload) => WithLock("managed-area.rename", () =>
    {
        EnsureWritable();
        return _managedAreas.Rename(payload);
    });

    public JsonObject AddManagedAreaWorkspaces(JsonObject payload) => WithLock("managed-area.workspace.add", () =>
    {
        EnsureWritable();
        return _managedAreas.AddWorkspaces(payload);
    });

    public JsonObject RemoveManagedAreaWorkspaces(JsonObject payload) => WithLock("managed-area.workspace.remove", () =>
    {
        EnsureWritable();
        return _managedAreas.RemoveWorkspaces(payload);
    });

    public JsonObject MoveManagedAreaWorkspaces(JsonObject payload) => WithLock("managed-area.workspace.move", () =>
    {
        EnsureWritable();
        return _managedAreas.MoveWorkspaces(payload);
    });

    public JsonObject MergeManagedAreas(JsonObject payload) => WithLock("managed-area.merge", () =>
    {
        EnsureWritable();
        return _managedAreas.Merge(payload);
    });

    public JsonObject DissolveManagedArea(JsonObject payload) => WithLock("managed-area.dissolve", () =>
    {
        EnsureWritable();
        return _managedAreas.Dissolve(payload);
    });

    public JsonObject DeleteManagedAreaAndMoveWorkspaces(JsonObject payload) => WithLock("managed-area.delete-and-move", () =>
    {
        EnsureWritable();
        return _managedAreas.DeleteAndMove(payload);
    });

    public JsonObject SavePosition(JsonObject payload) => WithLock("seat.move", () =>
    {
        EnsureWritable();
        return _assignments.SavePosition(payload);
    });

    public JsonObject CreateSeat(JsonObject payload) => WithLock("seat.create", () =>
    {
        EnsureWritable();
        return _assignments.CreateSeat(payload, _managedAreas.CreateSeatInArea);
    });

    public JsonObject DeleteSeat(JsonObject payload) => WithLock("seat.delete", () =>
    {
        EnsureWritable();
        return _assignments.DeleteSeat(payload, _managedAreas.EnsureWorkspaceIsNotManaged);
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
                TransactionCoordinator.StampRevision(scenarios, CurrentRevisionUnlocked());
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
        _managedAreas.ValidateAgainstMaps(real["maps"]!.AsObject());
        var documents = RealDocuments(real);
        documents["scenarios.json"] = scenarios;
        var scenarioName = Text(scenario["name"]);
        _transactions.Execute(documents, documents.Keys.Append("events.json"), "Antes de aplicar escenario " + scenarioName, "Escenario aplicado", scenarioName, sourceRevision);
        return new JsonObject { ["applied"] = selected.Count, ["remaining"] = Diff(baseState, draft, operations).Count };
    });

    public JsonObject GetEvents() => WithLock("events.read", () => new JsonObject { ["events"] = (ReadOptional("events.json")?["events"]?.DeepClone() ?? new JsonArray()) });

    public JsonObject GetBackups() => WithLock("backup.list", () =>
    {
        var backups = new JsonArray();
        foreach (var container in _backups.Containers()) backups.Add(container.Manifest);
        return new JsonObject { ["backups"] = backups };
    });

    public JsonObject GetBackupRetentionReport() => WithLock("backup.retention.report", () =>
    {
        var mode = BackupRetention.ParseMode(_config.BackupRetentionMode);
        if (mode != BackupRetentionMode.Report) throw new InvalidOperationException("El informe de retención requiere backupRetentionMode=report.");

        var report = BackupRetentionReport.Build(
            _backups.Containers(),
            ReadOptional("events.json")?["events"]?.AsArray(),
            DateTimeOffset.UtcNow);
        var document = _backups.RetentionReportDocument(report);
        _backups.WriteRetentionReport(document, report.TotalBackups, report.ReclaimableBytes);
        return document;
    });

    public JsonObject GetIntegrityReport() => WithLock("integrity.report", _reports.GetIntegrityReport);

    public JsonObject RestoreBackup(JsonObject payload) => WithLock("backup.restore", () =>
    {
        EnsureWritable();
        var id = Required(payload, "backupId", "Backup inválido.");
        var folder = _backups.ResolvePath(id);
        if (!_backups.Exists(folder)) throw new DirectoryNotFoundException("No existe el backup seleccionado.");
        var files = _backups.Files(folder);
        var restoredFiles = files.Where(file => UserRestoreFiles.Contains(file, StringComparer.OrdinalIgnoreCase)).ToArray();
        var documents = _backups.LoadDocuments(folder, restoredFiles);
        var restoredMaps = documents.GetValueOrDefault("maps.json") ?? ReadRequired("maps.json");
        var restoredManagedAreas = documents.GetValueOrDefault(ManagedAreas.FileName) ?? ReadOptional(ManagedAreas.FileName) ?? ManagedAreas.EmptyDocument();
        ManagedAreas.Normalize(restoredManagedAreas, restoredMaps);
        _transactions.Execute(documents, restoredFiles.Union(["events.json"], StringComparer.OrdinalIgnoreCase), "Antes de restaurar " + id, "Backup restaurado", id, CurrentRevisionUnlocked());
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
            TransactionCoordinator.StampRevision(document, CurrentRevisionUnlocked());
            WriteAtomic(ScenariosPath, document);
            return new JsonObject { ["ok"] = true, ["scope"] = "scenario" };
        }

        EnsureWritable();
        var currentEvents = ReadOptional("events.json") ?? New("events");
        var currentEntries = currentEvents["events"]?.AsArray().OfType<JsonObject>().ToList() ?? [];
        var last = currentEntries.LastOrDefault(item => Text(item["backupId"]).Length > 0 && Text(item["undoOf"]).Length == 0 && Text(item["undoneAt"]).Length == 0)
            ?? throw new InvalidOperationException("No hay más cambios reales reversibles.");
        var backupId = Text(last["backupId"]);
        var folder = _backups.ResolvePath(backupId);
        if (!_backups.Exists(folder)) throw new DirectoryNotFoundException("No está disponible el backup del último cambio.");
        var files = _backups.TransactionFiles(folder);
        var restoredFiles = files.Where(file => !string.Equals(file, "events.json", StringComparison.OrdinalIgnoreCase)).ToArray();
        var documents = _backups.LoadDocuments(folder, restoredFiles);
        last["undoneAt"] = DateTimeOffset.UtcNow.ToString("O");
        documents["events.json"] = currentEvents;
        _transactions.Execute(documents, restoredFiles.Union(["events.json"], StringComparer.OrdinalIgnoreCase), "Antes de deshacer " + Text(last["title"]), "Cambio deshecho", Text(last["title"]), CurrentRevisionUnlocked(), Text(last["id"]));
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
                    TransactionCoordinator.StampRevision(scenarios, CurrentRevisionUnlocked());
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
        TransactionCoordinator.StampRevision(document, CurrentRevisionUnlocked());
        WriteAtomic(ScenariosPath, document);
        return new JsonObject { ["ok"] = true };
    }

    private void CommitRealUnlocked(JsonObject state, string action, string description)
    {
        Bump(state["assignments"]!.AsObject());
        _transactions.Execute(RealDocuments(state), RealFiles, "Antes de " + action, action, description, CurrentRevisionUnlocked(), seatId: description);
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


    private T WithLock<T>(string action, Func<T> operation)
    {
        try
        {
            using var heldLock = AcquireLock();
            EnsureStateUnlocked();
            _transactions.RecoverPending();
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
            ["readOnly"] = _config.ReadOnly,
            ["scenarios"] = new JsonArray(scenarios["scenarios"]?.AsArray().OfType<JsonObject>().Select(s => new JsonObject { ["id"] = s["id"]?.DeepClone(), ["name"] = s["name"]?.DeepClone(), ["createdAt"] = s["createdAt"]?.DeepClone(), ["createdBy"] = s["createdBy"]?.DeepClone() }).ToArray() ?? []),
            ["activeScenario"] = active is null ? null : new JsonObject { ["id"] = active["id"]?.DeepClone(), ["name"] = active["name"]?.DeepClone(), ["undoCount"] = active["undo"]?.AsArray().Count ?? 0, ["isPrimary"] = active["isPrimary"]?.DeepClone() ?? false }
        };
    }


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
            ["introduced"] = new JsonArray(comparison.ValidationImpact.Introduced.Select(ReportService.ValidationJson).ToArray()),
            ["resolved"] = new JsonArray(comparison.ValidationImpact.Resolved.Select(ReportService.ValidationJson).ToArray()),
            ["persistent"] = new JsonArray(comparison.ValidationImpact.Persistent.Select(ReportService.ValidationJson).ToArray())
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
    private static string Cell(double x, double y) => AssignmentService.Cell(x, y);
    private static bool Coordinate(JsonNode? node, out double value) { value = 0; return node is JsonValue json && json.TryGetValue<double>(out value); }

    private JsonObject ScenariosUnlocked() => ReadOptional("scenarios.json") ?? New("scenarios");
    private JsonObject FindScenarioUnlocked(string id) => ScenariosUnlocked()["scenarios"]?.AsArray().OfType<JsonObject>().FirstOrDefault(s => Text(s["id"]) == id) ?? throw new InvalidDataException("Escenario inexistente.");
    private string ScenariosPath => DataPath("scenarios.json");


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
    private static bool TryRevision(JsonNode? node, out long revision)
    {
        revision = 0;
        return node is JsonValue value && value.TryGetValue<long>(out revision) && revision >= 0;
    }
    private static long? ReadRevision(string path) { var document = ReadJson(path); return document is not null && TryRevision(document["stateRevision"], out var revision) ? revision : null; }
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
