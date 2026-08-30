using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;

namespace PlanoOpenSpaceIT.Windows;

internal sealed class WebViewBridge
{
    private readonly DataStore _store;

    internal WebViewBridge(DataStore store) => _store = store;

    internal JsonNode Dispatch(string action, JsonObject payload)
    {
        var auditAction = IsSupportedAction(action) ? action : "unsupported";
        var stopwatch = Stopwatch.StartNew();
        using var actionScope = _store.BeginBridgeAction(auditAction);
        try
        {
            var result = DispatchCore(action, payload);
            _store.LogBridgeAction(auditAction, success: true, stopwatch.ElapsedMilliseconds, ScenarioIdForLog(action, payload, result));
            return result;
        }
        catch
        {
            _store.LogBridgeAction(auditAction, success: false, stopwatch.ElapsedMilliseconds, ScenarioIdForLog(action, payload));
            throw;
        }
    }

    private static bool IsSupportedAction(string action) => action is
        "loadInitialData" or "reloadData" or "createScenario" or "deleteScenario" or
        "saveAssignment" or "deleteAssignment" or "bulkUpdateAssignments" or "saveSeatPosition" or "createSeat" or
        "deleteSeat" or "getScenarioDiff" or "applyScenario" or "getEvents" or
        "getBackups" or "getBackupRetentionReport" or "getIntegrityReport" or "restoreBackup" or "getUndoPreview" or "undoLastChange" or
        "createManagedArea" or "renameManagedArea" or
        "addManagedAreaWorkspaces" or "addWorkspacesToManagedArea" or
        "removeManagedAreaWorkspaces" or "removeWorkspacesFromManagedArea" or
        "moveManagedAreaWorkspaces" or "moveWorkspacesBetweenManagedAreas" or
        "mergeManagedAreas" or "dissolveManagedArea" or
        "deleteManagedAreaAndMoveWorkspaces" or "deleteAndMoveManagedArea" or
        "reportPlanResourceDiagnostic" or "runValidation" or "runSpatialAnalytics" or "runMovementPlanner" or "createScenarioFromMovementPlan" or "exportExcel";

    private static string? ScenarioIdForLog(string action, JsonObject payload, JsonNode? result = null)
    {
        if (action is "createScenario" or "createScenarioFromMovementPlan") return result?["scenarioId"]?.GetValue<string>();

        return action is "loadInitialData" or "reloadData" or "deleteScenario" or
            "saveAssignment" or "deleteAssignment" or "bulkUpdateAssignments" or "saveSeatPosition" or "createSeat" or
            "deleteSeat" or "getScenarioDiff" or "applyScenario" or "getUndoPreview" or
            "undoLastChange" or "runValidation" or "runSpatialAnalytics" or "runMovementPlanner"
            ? payload["scenarioId"]?.GetValue<string>()
            : null;
    }

    private JsonNode DispatchCore(string action, JsonObject payload) => action switch
    {
        "loadInitialData" or "reloadData" => _store.Load(payload["scenarioId"]?.GetValue<string>()),
        "createScenario" => _store.CreateScenario(payload),
        "deleteScenario" => _store.DeleteScenario(payload),
        "saveAssignment" => _store.SaveAssignment(payload, delete: false),
        "deleteAssignment" => _store.SaveAssignment(payload, delete: true),
        "bulkUpdateAssignments" => _store.BulkUpdateAssignments(payload),
        "saveSeatPosition" => _store.SavePosition(payload),
        "createSeat" => _store.CreateSeat(payload),
        "deleteSeat" => _store.DeleteSeat(payload),
        "getScenarioDiff" => _store.GetScenarioDiff(payload),
        "applyScenario" => _store.ApplyScenario(payload),
        "getEvents" => _store.GetEvents(),
        "getBackups" => _store.GetBackups(),
        "getBackupRetentionReport" => _store.GetBackupRetentionReport(),
        "getIntegrityReport" => _store.GetIntegrityReport(),
        "restoreBackup" => _store.RestoreBackup(payload),
        "getUndoPreview" => _store.GetUndoPreview(payload),
        "undoLastChange" => _store.UndoLastChange(payload),
        "createManagedArea" => _store.CreateManagedArea(payload),
        "renameManagedArea" => _store.RenameManagedArea(payload),
        "addManagedAreaWorkspaces" or "addWorkspacesToManagedArea" => _store.AddManagedAreaWorkspaces(payload),
        "removeManagedAreaWorkspaces" or "removeWorkspacesFromManagedArea" => _store.RemoveManagedAreaWorkspaces(payload),
        "moveManagedAreaWorkspaces" or "moveWorkspacesBetweenManagedAreas" => _store.MoveManagedAreaWorkspaces(payload),
        "mergeManagedAreas" => _store.MergeManagedAreas(payload),
        "dissolveManagedArea" => _store.DissolveManagedArea(payload),
        "deleteManagedAreaAndMoveWorkspaces" or "deleteAndMoveManagedArea" => _store.DeleteManagedAreaAndMoveWorkspaces(payload),
        "reportPlanResourceDiagnostic" => _store.ReportPlanResourceDiagnostic(payload),
        "runValidation" => _store.RunValidation(payload["scenarioId"]?.GetValue<string>()),
        "runSpatialAnalytics" => _store.RunSpatialAnalytics(payload["scenarioId"]?.GetValue<string>()),
        "runMovementPlanner" => _store.RunMovementPlanner(payload),
        "createScenarioFromMovementPlan" => _store.CreateScenarioFromMovementPlan(payload),
        "exportExcel" => _store.ExportExcel(payload["exportFolder"]?.GetValue<string>() ?? throw new InvalidDataException("Falta la carpeta de exportación.")),
        _ => throw new InvalidOperationException($"Acción no soportada: {action}")
    };
}
