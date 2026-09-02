using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using PlanoOpenSpaceIT.Windows;
using Xunit;

namespace PlanoOpenSpaceIT.Desktop.Tests;

public sealed class ReleaseReadinessTests
{

[Fact]
    public void TrustedWebViewOrigin()
{
    Xunit.Assert.True(MainWindow.IsTrustedWebMessageSource("https://plano.local/index.html"), "The local virtual host is trusted.");
    Xunit.Assert.True(!MainWindow.IsTrustedWebMessageSource("https://example.invalid/index.html"), "External origins are rejected.");
    Xunit.Assert.True(!MainWindow.IsTrustedWebMessageSource("file:///tmp/index.html"), "File origins are rejected.");
}

[Fact]
    public async Task WindowInitializationIsIdempotent()
{
    var storeCount = 0;
    var bridgeCount = 0;
    var lifecycleStartCount = 0;
    var subscriptionCount = 0;
    var webViewInitializationCount = 0;
    var initialization = new WindowInitialization<object, object>(
        () => { storeCount++; return new object(); },
        _ => { bridgeCount++; return new object(); },
        _ => lifecycleStartCount++,
        () => subscriptionCount++);

    Task InitializeWebView(Action subscribe)
    {
        webViewInitializationCount++;
        subscribe();
        return Task.CompletedTask;
    }

    await initialization.InitializeAsync(InitializeWebView);
    var firstStore = initialization.Store;
    var firstBridge = initialization.Bridge;
    await initialization.InitializeAsync(InitializeWebView);

    Xunit.Assert.Equal(1, storeCount);
    Xunit.Assert.Equal(1, bridgeCount);
    Xunit.Assert.Equal(1, lifecycleStartCount);
    Xunit.Assert.Equal(1, subscriptionCount);
    Xunit.Assert.Equal(1, webViewInitializationCount);
    Xunit.Assert.True(ReferenceEquals(firstStore, initialization.Store), "The DataStore instance remains stable.");
    Xunit.Assert.True(ReferenceEquals(firstBridge, initialization.Bridge), "The WebView bridge instance remains stable.");
}

[Fact]
    public async Task WindowInitializationRetriesWebViewWithoutDuplicatingSession()
{
    var storeCount = 0;
    var bridgeCount = 0;
    var lifecycleStartCount = 0;
    var subscriptionCount = 0;
    var webViewInitializationCount = 0;
    var initialization = new WindowInitialization<object, object>(
        () => { storeCount++; return new object(); },
        _ => { bridgeCount++; return new object(); },
        _ => lifecycleStartCount++,
        () => subscriptionCount++);

    Task FailAfterSubscription(Action subscribe)
    {
        webViewInitializationCount++;
        subscribe();
        return Task.FromException(new InvalidOperationException("WebView2 no disponible."));
    }

    try { await initialization.InitializeAsync(FailAfterSubscription); }
    catch (InvalidOperationException) { }

    await initialization.InitializeAsync(subscribe =>
    {
        webViewInitializationCount++;
        subscribe();
        return Task.CompletedTask;
    });

    Xunit.Assert.Equal(1, storeCount);
    Xunit.Assert.Equal(1, bridgeCount);
    Xunit.Assert.Equal(1, lifecycleStartCount);
    Xunit.Assert.Equal(1, subscriptionCount);
    Xunit.Assert.Equal(2, webViewInitializationCount);
}

[Fact]
    public void UserPreferencesPreserveKeyboardShortcutChoice()
{
    var folder = Path.Combine(Path.GetTempPath(), "PlanoOpenSpaceITPreferences", Guid.NewGuid().ToString("N"));
    var preferencesPath = Path.Combine(folder, "user-preferences.json");
    try
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(preferencesPath, "{\"exportFolder\":\"C:\\\\exports\",\"skipExportFolderPrompt\":true,\"theme\":\"penpot-dark\"}");

        var store = new UserPreferencesStore(preferencesPath);
        Xunit.Assert.True(store.LoadSingleKeyShortcutsEnabled(), "Profiles without the shortcut preference enable it by default.");
        store.SaveUserPreferences(null, false);

        var reloaded = new UserPreferencesStore(preferencesPath);
        var saved = reloaded.Load();
        Xunit.Assert.Equal("C:\\exports", saved.ExportFolder);
        Xunit.Assert.True(saved.SkipExportFolderPrompt, "Saving shortcuts preserves export folder behavior.");
        Xunit.Assert.Equal("penpot-dark", reloaded.LoadTheme());
        Xunit.Assert.True(!reloaded.LoadSingleKeyShortcutsEnabled(), "The shortcut choice survives a reload.");
    }
    finally
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }
}

[Fact]
    public void WindowGeometryPreservesValidRectangle()
{
    var primary = new WindowBounds(0, 0, 1600, 1000);
    var saved = new WindowBounds(120, 80, 1000, 700);
    Xunit.Assert.Equal(saved, WindowGeometry.Clamp(saved, new[] { primary }, primary));
}

[Fact]
    public void WindowGeometryFallsBackToPrimaryMonitor()
{
    var primary = new WindowBounds(0, 0, 1600, 1000);
    var secondary = new WindowBounds(1600, 0, 1200, 1000);
    var disconnected = new WindowBounds(4200, 100, 1000, 700);
    Xunit.Assert.Equal(new WindowBounds(100, 50, 1400, 900), WindowGeometry.Clamp(disconnected, new[] { primary, secondary }, primary));
}

[Fact]
    public void WindowGeometryFitsOversizedRectangle()
{
    var primary = new WindowBounds(0, 0, 1600, 900);
    var oversized = new WindowBounds(100, 100, 2400, 1400);
    Xunit.Assert.Equal(new WindowBounds(0, 0, 1600, 900), WindowGeometry.Clamp(oversized, new[] { primary }, primary));
}

[Fact]
    public void WindowGeometryHandlesInvalidNumbers()
{
    var primary = new WindowBounds(0, 0, 1600, 1000);
    var invalid = new WindowBounds(double.NaN, 0, double.PositiveInfinity, 700);
    Xunit.Assert.Equal(new WindowBounds(100, 50, 1400, 900), WindowGeometry.Clamp(invalid, new[] { primary }, primary));
}

[Fact]
    public void WindowGeometryRepositionsNegativeEdges()
{
    var primary = new WindowBounds(0, 0, 1600, 1000);
    var partial = new WindowBounds(-120, -40, 800, 600);
    Xunit.Assert.Equal(new WindowBounds(0, 0, 800, 600), WindowGeometry.Clamp(partial, new[] { primary }, primary));
}

[Fact]
    public void WindowGeometryKeepsSecondaryMonitorPlacement()
{
    var primary = new WindowBounds(0, 0, 1600, 1000);
    var secondary = new WindowBounds(1600, 0, 1280, 1024);
    var saved = new WindowBounds(1800, 120, 1000, 800);
    Xunit.Assert.Equal(saved, WindowGeometry.Clamp(saved, new[] { primary, secondary }, primary));
}

[Fact]
    public void WindowGeometryHandlesNoMonitors()
{
    var saved = new WindowBounds(100, 100, 1000, 700);
    Xunit.Assert.Equal(WindowGeometry.DefaultBounds, WindowGeometry.Clamp(saved, [], default));
}

[Fact]
    public void RealityLoadAndDisplayLocations()
{
    using var fixture = new Fixture();
    var package = fixture.Store.Load();
    var maps = package["maps"]?["maps"]?.AsArray().OfType<JsonObject>().ToArray() ?? [];
    Xunit.Assert.Equal(2, maps.Length);
    var locations = maps.SelectMany(map => map["seats"]?.AsArray().OfType<JsonObject>() ?? [])
        .Select(seat => seat["displayLocation"]?.GetValue<string>())
        .ToArray();
    Xunit.Assert.True(locations.All(location => !string.IsNullOrWhiteSpace(location)), "Every seat has a display location.");
    Xunit.Assert.Equal(locations.Length, locations.Distinct(StringComparer.Ordinal).Count());
}

[Fact]
    public void ScenarioDiffAndValidation()
{
    using var fixture = new Fixture();
    var created = fixture.Store.CreateScenario(new JsonObject { ["name"] = "Cambio aislado" });
    var scenarioId = Text(created["scenarioId"]);
    fixture.Store.SaveAssignment(new JsonObject { ["scenarioId"] = scenarioId, ["workstationId"] = "N-02", ["personId"] = "person-3", ["status"] = "confirmed" }, false);
    var diff = fixture.Store.GetScenarioDiff(new JsonObject { ["scenarioId"] = scenarioId });
    Xunit.Assert.True((diff["changes"]?.AsArray().Count ?? 0) > 0, "The isolated edit produces a scenario diff.");
    var validation = fixture.Store.RunValidation(scenarioId);
    Xunit.Assert.True(validation["summary"] is JsonObject, "Validation runs on the effective scenario state.");
    Xunit.Assert.Equal("N-01", AssignmentSeat(fixture.ReadJson("assignments.json"), "person-1"));
}

[Fact]
    public void PlannerScenarioKeepsRealityUnchanged()
{
    using var fixture = new Fixture();
    var before = fixture.Read("assignments.json");
    var created = fixture.Store.CreateScenarioFromMovementPlan(new JsonObject
    {
        ["name"] = "Plan de prueba",
        ["requests"] = new JsonArray(new JsonObject { ["sourceWorkspaceId"] = "N-01", ["destinationWorkspaceId"] = "N-02" })
    });
    var scenarioId = Text(created["scenarioId"]);
    Xunit.Assert.Equal(before, fixture.Read("assignments.json"));
    var operations = fixture.ReadJson("scenarios.json")["scenarios"]?.AsArray().OfType<JsonObject>().Single(item => Text(item["id"]) == scenarioId)?["operations"]?.AsArray().OfType<JsonObject>().ToArray()
        ?? throw new InvalidOperationException("Planner scenario operations are missing.");
    Xunit.Assert.Single(operations);
    Xunit.Assert.Equal($"movement|{scenarioId}|norte|N-01|norte|N-02", Text(operations[0]["id"]));
    Xunit.Assert.Equal("movement", Text(operations[0]["type"]));
    Xunit.Assert.True(operations[0]["atomic"]?.GetValue<bool>() == true, "Persisted movement operation is atomic.");
    Xunit.Assert.Equal(new[] { "assignment|N-01", "assignment|N-02" }, operations[0]["members"]?.AsArray().Select(Text) ?? []);
    var effective = fixture.Store.Load(scenarioId);
    Xunit.Assert.Equal("N-02", AssignmentSeat(effective["assignments"]!.AsObject(), "person-1"));
}

[Fact]
    public void LegacyPlannerScenarioMovesVerifiedSeatPayloadAtomically()
{
    using var fixture = new Fixture(legacySource: true);
    var mapsBefore = fixture.ReadJson("maps.json");
    var assignmentsBefore = fixture.ReadJson("assignments.json");
    var positionsBefore = fixture.ReadJson("positions.json");
    var created = fixture.Store.CreateScenarioFromMovementPlan(new JsonObject
    {
        ["name"] = "Movimiento heredado",
        ["requests"] = new JsonArray(new JsonObject { ["sourceWorkspaceId"] = "N-01", ["destinationWorkspaceId"] = "N-02" })
    });
    var scenarioId = Text(created["scenarioId"]);

    TestAssertions.EqualJson(mapsBefore, fixture.ReadJson("maps.json"), "Legacy planner creation leaves reality maps unchanged.");
    TestAssertions.EqualJson(assignmentsBefore, fixture.ReadJson("assignments.json"), "Legacy planner creation leaves reality assignments unchanged.");
    TestAssertions.EqualJson(positionsBefore, fixture.ReadJson("positions.json"), "Legacy planner creation leaves reality positions unchanged.");

    var draft = fixture.Store.Load(scenarioId);
    var source = Workspace(draft, "norte", "N-01");
    var destination = Assignment(draft["assignments"]!.AsObject(), "N-02");
    Xunit.Assert.Equal("", Text(source["personId"]));
    Xunit.Assert.Equal("", Text(source["deviceName"]));
    Xunit.Assert.Equal("person-1", Text(destination["personId"]));
    Xunit.Assert.Equal("device-legacy-1", Text(destination["deviceId"]));
    Xunit.Assert.Equal("legacy-destination-roseta", Text(destination["roseta"]));
    Xunit.Assert.True(destination["locationId"] is null && destination["reference"] is null, "Legacy destination assignments do not inherit source location or reference data.");
    Xunit.Assert.Single(draft["assignments"]?["assignments"]?.AsArray().OfType<JsonObject>().Where(item => Text(item["personId"]) == "person-1") ?? []);

    var members = AtomicMembers(ScenarioChanges(fixture, scenarioId), "seat|norte|N-01");
    Xunit.Assert.Equal(new[] { "assignment|N-02", "seat|norte|N-01" }, members.Select(change => Text(change["id"])));
    var beforePartial = fixture.DataHashes();
    var partialError = Capture(() => fixture.Store.ApplyScenario(new JsonObject { ["scenarioId"] = scenarioId, ["changeIds"] = ChangeIds(members.Take(1)) }));
    Xunit.Assert.True(partialError.Contains("atomic-operation-incomplete", StringComparison.Ordinal), "A half legacy movement is rejected.");
    TestAssertions.EqualHashes(beforePartial, fixture.DataHashes(), "Rejected legacy half movement leaves all persisted data unchanged.");

    Xunit.Assert.Equal(0, fixture.Store.RunValidation(scenarioId)["count"]?.GetValue<int>() ?? -1);
    fixture.Store.ApplyScenario(new JsonObject { ["scenarioId"] = scenarioId, ["changeIds"] = ChangeIds(members) });
    var appliedMaps = fixture.ReadJson("maps.json");
    var appliedAssignments = fixture.ReadJson("assignments.json");
    var appliedSource = Workspace(new JsonObject { ["maps"] = appliedMaps }, "norte", "N-01");
    Xunit.Assert.Equal("", Text(appliedSource["personId"]));
    Xunit.Assert.Equal("", Text(appliedSource["deviceName"]));
    Xunit.Assert.Equal("N-02", AssignmentSeat(appliedAssignments, "person-1"));
    Xunit.Assert.Equal(0, fixture.Store.RunValidation()["count"]?.GetValue<int>() ?? -1);

    fixture.Store.UndoLastChange(new JsonObject());
    TestAssertions.EqualDocumentContent(mapsBefore, fixture.ReadJson("maps.json"), "Undo restores the legacy source seat exactly.");
    TestAssertions.EqualDocumentContent(assignmentsBefore, fixture.ReadJson("assignments.json"), "Undo removes the legacy destination assignment exactly.");
    TestAssertions.EqualDocumentContent(positionsBefore, fixture.ReadJson("positions.json"), "Undo restores positions exactly.");
}

[Fact]
    public void LegacyPlannerSupportsPersonOnlySource()
{
    using var fixture = new Fixture(legacySource: true, legacyDevice: false);
    var created = fixture.Store.CreateScenarioFromMovementPlan(new JsonObject
    {
        ["name"] = "Movimiento heredado sin equipo",
        ["requests"] = new JsonArray(new JsonObject { ["sourceWorkspaceId"] = "N-01", ["destinationWorkspaceId"] = "N-02" })
    });
    var draft = fixture.Store.Load(Text(created["scenarioId"]));
    var destination = Assignment(draft["assignments"]!.AsObject(), "N-02");
    Xunit.Assert.True(destination["deviceId"] is null, "A legacy source without a device creates a person-only destination assignment.");
    Xunit.Assert.Equal(0, fixture.Store.RunValidation(Text(created["scenarioId"]))["count"]?.GetValue<int>() ?? -1);
}

[Fact]
    public void AnalyticsUsesScenarioState()
{
    using var fixture = new Fixture();
    var created = fixture.Store.CreateScenarioFromMovementPlan(new JsonObject
    {
        ["name"] = "Analítica de prueba",
        ["requests"] = new JsonArray(new JsonObject { ["sourceWorkspaceId"] = "N-01", ["destinationWorkspaceId"] = "N-02" })
    });
    var analytics = fixture.Store.RunSpatialAnalytics(Text(created["scenarioId"]));
    Xunit.Assert.True(analytics["result"]?["totals"] is JsonObject, "Scenario analytics returns totals.");
    Xunit.Assert.True(analytics["baseline"] is JsonObject, "Scenario analytics returns its base for Compare.");
}

[Fact]
    public void AtomicMovementAppliesAsWholeOperation()
{
    using var fixture = new Fixture();
    var created = fixture.Store.CreateScenarioFromMovementPlan(new JsonObject
    {
        ["name"] = "Aplicación atómica",
        ["requests"] = new JsonArray(new JsonObject { ["sourceWorkspaceId"] = "N-01", ["destinationWorkspaceId"] = "N-02" })
    });
    var scenarioId = Text(created["scenarioId"]);
    var changes = ScenarioChanges(fixture, scenarioId);
    var members = AtomicMembers(changes, "assignment|N-01");

    var applied = fixture.Store.ApplyScenario(new JsonObject { ["scenarioId"] = scenarioId, ["changeIds"] = ChangeIds(members) });

    Xunit.Assert.Equal(2, applied["applied"]?.GetValue<int>() ?? 0);
    Xunit.Assert.Equal(0, applied["remaining"]?.GetValue<int>() ?? 0);
    Xunit.Assert.Equal("N-02", AssignmentSeat(fixture.ReadJson("assignments.json"), "person-1"));
    Xunit.Assert.True((fixture.Store.GetBackups()["backups"]?.AsArray().Count ?? 0) > 0, "Apply creates a recoverable backup.");
    Xunit.Assert.True((fixture.Store.GetEvents()["events"]?.AsArray().Count ?? 0) > 0, "Apply writes audit history.");
}

[Fact]
    public void AtomicMovementRejectsHalfGroupWithoutWrites()
{
    using var fixture = new Fixture();
    var created = fixture.Store.CreateScenarioFromMovementPlan(new JsonObject
    {
        ["name"] = "Rechazo atómico",
        ["requests"] = new JsonArray(new JsonObject { ["sourceWorkspaceId"] = "N-01", ["destinationWorkspaceId"] = "N-02" })
    });
    var scenarioId = Text(created["scenarioId"]);
    var members = AtomicMembers(ScenarioChanges(fixture, scenarioId), "assignment|N-01");
    var before = fixture.Read("assignments.json");

    var error = Capture(() => fixture.Store.ApplyScenario(new JsonObject { ["scenarioId"] = scenarioId, ["changeIds"] = ChangeIds(members.Take(1)) }));

    Xunit.Assert.True(error.Contains("atomic-operation-incomplete", StringComparison.Ordinal), "A partial movement is rejected with the atomic-operation-incomplete error.");
    Xunit.Assert.Equal(before, fixture.Read("assignments.json"));
    Xunit.Assert.Equal(2, ScenarioChanges(fixture, scenarioId).Length);
}

[Fact]
    public void AtomicMovementRejectsMixedPartialSelection()
{
    using var fixture = new Fixture();
    var created = fixture.Store.CreateScenarioFromMovementPlan(new JsonObject
    {
        ["name"] = "Rechazo mixto",
        ["requests"] = new JsonArray(new JsonObject { ["sourceWorkspaceId"] = "N-01", ["destinationWorkspaceId"] = "N-02" })
    });
    var scenarioId = Text(created["scenarioId"]);
    fixture.Store.SaveAssignment(new JsonObject { ["scenarioId"] = scenarioId, ["workstationId"] = "S-02", ["personId"] = "person-3", ["status"] = "confirmed" }, false);
    var members = AtomicMembers(ScenarioChanges(fixture, scenarioId), "assignment|N-01");
    var independent = ScenarioChanges(fixture, scenarioId).Single(change => Text(change["id"]) == "assignment|S-02");
    var before = fixture.Read("assignments.json");

    var error = Capture(() => fixture.Store.ApplyScenario(new JsonObject { ["scenarioId"] = scenarioId, ["changeIds"] = ChangeIds(new[] { members[0], independent }) }));

    Xunit.Assert.True(error.Contains("atomic-operation-incomplete", StringComparison.Ordinal), "Independent edits cannot make a partial atomic movement valid.");
    Xunit.Assert.Equal(before, fixture.Read("assignments.json"));
    Xunit.Assert.True(string.IsNullOrEmpty(AssignmentSeat(fixture.ReadJson("assignments.json"), "person-3")), "The selected independent edit is not written after rejection.");
}

[Fact]
    public void WholeAtomicGroupAppliesWithIndependentEdits()
{
    using var fixture = new Fixture();
    var created = fixture.Store.CreateScenarioFromMovementPlan(new JsonObject
    {
        ["name"] = "Operaciones y edición independiente",
        ["requests"] = new JsonArray(
            new JsonObject { ["sourceWorkspaceId"] = "N-01", ["destinationWorkspaceId"] = "N-02" },
            new JsonObject { ["sourceWorkspaceId"] = "S-01", ["destinationWorkspaceId"] = "S-02" })
    });
    var scenarioId = Text(created["scenarioId"]);
    fixture.Store.SavePosition(new JsonObject { ["scenarioId"] = scenarioId, ["mapId"] = "norte", ["seatId"] = "N-01", ["x"] = .35, ["y"] = .20 });
    var changes = ScenarioChanges(fixture, scenarioId);
    var selectedOperation = AtomicMembers(changes, "assignment|N-01");
    var independent = changes.Single(change => Text(change["id"]) == "seat|norte|N-01");

    var applied = fixture.Store.ApplyScenario(new JsonObject { ["scenarioId"] = scenarioId, ["changeIds"] = ChangeIds(selectedOperation.Append(independent)) });

    Xunit.Assert.Equal(3, applied["applied"]?.GetValue<int>() ?? 0);
    Xunit.Assert.Equal(2, applied["remaining"]?.GetValue<int>() ?? 0);
    Xunit.Assert.Equal("N-02", AssignmentSeat(fixture.ReadJson("assignments.json"), "person-1"));
    var remaining = ScenarioChanges(fixture, scenarioId);
    Xunit.Assert.Equal(2, remaining.Length);
    Xunit.Assert.True(remaining.All(change => Text(change["operationId"]) == Text(remaining[0]["operationId"]) && change["atomic"]?.GetValue<bool>() == true), "Both pending members remain in the same atomic operation.");
    Xunit.Assert.Equal(new[] { "assignment|S-01", "assignment|S-02" }, remaining.Select(change => Text(change["id"])).Order(StringComparer.Ordinal));
}

[Fact]
    public void StaleScenarioApplyRejectsWithoutMutatingReality()
{
    using var fixture = new Fixture();
    var created = fixture.Store.CreateScenarioFromMovementPlan(new JsonObject
    {
        ["name"] = "Conflicto de revisión",
        ["requests"] = new JsonArray(new JsonObject { ["sourceWorkspaceId"] = "N-01", ["destinationWorkspaceId"] = "N-02" })
    });
    var scenarioId = Text(created["scenarioId"]);
    var changeId = Text(fixture.Store.GetScenarioDiff(new JsonObject { ["scenarioId"] = scenarioId })["changes"]?.AsArray().OfType<JsonObject>().First()? ["id"]);
    fixture.Store.SaveAssignment(new JsonObject { ["workstationId"] = "S-02", ["personId"] = "person-3", ["status"] = "confirmed" }, false);
    var beforeApply = fixture.Read("assignments.json");

    var error = Capture(() => fixture.Store.ApplyScenario(new JsonObject { ["scenarioId"] = scenarioId, ["changeIds"] = new JsonArray(changeId) }));

    Xunit.Assert.True(error.Contains("La realidad cambió desde la creación del escenario", StringComparison.Ordinal), "A stale scenario Apply is rejected by baseRevision.");
    Xunit.Assert.Equal(beforeApply, fixture.Read("assignments.json"));
    Xunit.Assert.True((fixture.Store.GetScenarioDiff(new JsonObject { ["scenarioId"] = scenarioId })["changes"]?.AsArray().Count ?? 0) > 0, "Rejected Apply preserves the scenario draft for review.");
}

[Fact]
    public void RealUndoRestoresState()
{
    using var fixture = new Fixture();
    var created = fixture.Store.CreateScenarioFromMovementPlan(new JsonObject
    {
        ["name"] = "Undo de prueba",
        ["requests"] = new JsonArray(new JsonObject { ["sourceWorkspaceId"] = "N-01", ["destinationWorkspaceId"] = "N-02" })
    });
    var scenarioId = Text(created["scenarioId"]);
    var members = AtomicMembers(ScenarioChanges(fixture, scenarioId), "assignment|N-01");
    fixture.Store.ApplyScenario(new JsonObject { ["scenarioId"] = scenarioId, ["changeIds"] = ChangeIds(members) });
    fixture.Store.UndoLastChange(new JsonObject());
    Xunit.Assert.Equal("N-01", AssignmentSeat(fixture.ReadJson("assignments.json"), "person-1"));
}

[Fact]
    public void BulkReservationsAreEffectiveStateAtomic()
{
    VerifyQaBulk04MixedSelectionEvidence();
    VerifyQaBulk04ConcurrentRealityChangeIsAtomic();

    // Preserve the original five-effective-Free coverage independently of QA-BULK-04.
    using var fixture = new Fixture();
    fixture.WriteRaw("maps.json", new JsonObject
    {
        ["maps"] = new JsonArray(new JsonObject
        {
            ["id"] = "bulk", ["name"] = "Bulk", ["seats"] = new JsonArray(
                new JsonObject { ["id"] = "F-1", ["type"] = "occupied", ["x"] = .1, ["y"] = .1 },
                new JsonObject { ["id"] = "F-2", ["x"] = .2, ["y"] = .1 },
                new JsonObject { ["id"] = "F-3", ["x"] = .3, ["y"] = .1 },
                new JsonObject { ["id"] = "F-4", ["x"] = .4, ["y"] = .1 },
                new JsonObject { ["id"] = "F-5", ["x"] = .5, ["y"] = .1 },
                new JsonObject { ["id"] = "R-1", ["x"] = .6, ["y"] = .1 },
                new JsonObject { ["id"] = "O-1", ["x"] = .7, ["y"] = .1 })
        })
    }.ToJsonString());
    fixture.WriteRaw("assignments.json", new JsonObject
    {
        ["version"] = 7,
        ["assignments"] = new JsonArray(
            new JsonObject { ["workstationId"] = "F-2", ["status"] = "confirmed", ["notes"] = "preserve" },
            new JsonObject { ["workstationId"] = "F-3", ["status"] = "free" },
            new JsonObject { ["workstationId"] = "R-1", ["status"] = "reserved", ["notes"] = "reserved" },
            new JsonObject { ["workstationId"] = "O-1", ["status"] = "confirmed", ["personId"] = "person-bulk" })
    }.ToJsonString());

    var mapsBefore = fixture.ReadJson("maps.json");
    var assignmentsBefore = fixture.ReadJson("assignments.json");
    var positionsBefore = fixture.ReadJson("positions.json");
    var result = fixture.Store.BulkUpdateAssignments(new JsonObject
    {
        ["workstationIds"] = new JsonArray("F-1", "F-2", "F-3", "F-4", "F-5"),
        ["status"] = "reserved"
    });

    Xunit.Assert.Equal(5, result["updated"]?.GetValue<int>() ?? -1);
    Xunit.Assert.Equal(5, fixture.ReadJson("assignments.json")["assignments"]?.AsArray().OfType<JsonObject>().Count(item => Text(item["workstationId"]).StartsWith("F-", StringComparison.Ordinal) && Text(item["status"]) == "reserved") ?? 0);
    var events = fixture.Store.GetEvents()["events"]?.AsArray().OfType<JsonObject>().ToArray() ?? [];
    Xunit.Assert.Single(events);
    Xunit.Assert.Equal("Puestos reservados", Text(events[0]["title"]));
    Xunit.Assert.Equal("5 puestos", Text(events[0]["description"]));
    Xunit.Assert.Single(fixture.Store.GetBackups()["backups"]?.AsArray() ?? new JsonArray());

    fixture.Store.UndoLastChange(new JsonObject());
    TestAssertions.EqualDocumentContent(mapsBefore, fixture.ReadJson("maps.json"), "One Undo restores the exact mixed map snapshot.");
    TestAssertions.EqualDocumentContent(assignmentsBefore, fixture.ReadJson("assignments.json"), "One Undo restores the exact mixed assignment snapshot.");
    TestAssertions.EqualDocumentContent(positionsBefore, fixture.ReadJson("positions.json"), "One Undo restores the exact position snapshot.");

    var beforeInvalid = fixture.DataHashes();
    var backupsBeforeInvalid = fixture.Store.GetBackups()["backups"]?.AsArray().Count ?? 0;
    var invalidError = Capture(() => fixture.Store.BulkUpdateAssignments(new JsonObject { ["workstationIds"] = new JsonArray("F-1", "O-1"), ["status"] = "reserved" }));
    Xunit.Assert.True(invalidError.Contains("ocupado", StringComparison.OrdinalIgnoreCase), "A mixed request containing an occupied target is rejected.");
    TestAssertions.EqualHashes(beforeInvalid, fixture.DataHashes(), "The invalid mixed request is transactionally atomic and writes nothing.");
    Xunit.Assert.Equal(backupsBeforeInvalid, fixture.Store.GetBackups()["backups"]?.AsArray().Count ?? 0);
    Xunit.Assert.True(Capture(() => fixture.Store.GetUndoPreview(new JsonObject())).Contains("No hay más cambios", StringComparison.Ordinal), "The invalid request creates no Undo entry.");

    var beforeNoOp = fixture.DataHashes();
    var backupsBeforeNoOp = fixture.Store.GetBackups()["backups"]?.AsArray().Count ?? 0;
    var noOp = fixture.Store.BulkUpdateAssignments(new JsonObject { ["workstationIds"] = new JsonArray("F-1"), ["status"] = "confirmed" });
    Xunit.Assert.Equal(0, noOp["updated"]?.GetValue<int>() ?? -1);
    Xunit.Assert.True(noOp["noOp"]?.GetValue<bool>() == true, "The result identifies the no-op.");
    TestAssertions.EqualHashes(beforeNoOp, fixture.DataHashes(), "A no-op writes no files or history.");
    Xunit.Assert.Equal(backupsBeforeNoOp, fixture.Store.GetBackups()["backups"]?.AsArray().Count ?? 0);

    var empty = fixture.Store.BulkUpdateAssignments(new JsonObject { ["workstationIds"] = new JsonArray(), ["status"] = "reserved" });
    Xunit.Assert.Equal(0, empty["updated"]?.GetValue<int>() ?? -1);
    TestAssertions.EqualHashes(beforeNoOp, fixture.DataHashes(), "An empty target list writes nothing.");
}

static void VerifyQaBulk04MixedSelectionEvidence()
{
    using var fixture = new Fixture();
    WriteQaBulk04Reality(fixture);

    var selected = new[] { "F1", "F2", "R1", "O1", "F3" };
    var eligible = new[] { "F1", "F2", "F3" };
    var sent = eligible;
    var beforeAssignments = fixture.ReadJson("assignments.json");
    var beforeMaps = fixture.ReadJson("maps.json");
    var beforeBySeat = QaBulk04AssignmentSnapshots(beforeAssignments, selected);

    Xunit.Assert.Equal(new[] { "F1", "F2", "R1", "O1", "F3" }, selected);
    Xunit.Assert.Equal(new[] { "F1", "F2", "F3" }, eligible);
    Xunit.Assert.Equal(eligible, sent);
    Console.WriteLine($"QA-BULK-04 selected=[{string.Join(',', selected)}] eligible=[{string.Join(',', eligible)}] sent=[{string.Join(',', sent)}]");

    var result = fixture.Store.BulkUpdateAssignments(new JsonObject
    {
        ["workstationIds"] = new JsonArray(sent.Select(id => (JsonNode?)id).ToArray()),
        ["status"] = "reserved"
    });

    Xunit.Assert.Equal(3, result["updated"]?.GetValue<int>() ?? -1);
    var afterAssignments = fixture.ReadJson("assignments.json");
    var afterBySeat = QaBulk04AssignmentSnapshots(afterAssignments, selected);
    var changed = selected.Where(id => beforeBySeat[id] != afterBySeat[id]).ToArray();
    var unchanged = selected.Where(id => beforeBySeat[id] == afterBySeat[id]).ToArray();
    Xunit.Assert.Equal(eligible, changed);
    Xunit.Assert.Equal(new[] { "R1", "O1" }, unchanged);
    TestAssertions.EqualJson(Assignment(beforeAssignments, "R1"), Assignment(afterAssignments, "R1"), "QA-BULK-04 leaves R1 semantically unchanged.");
    TestAssertions.EqualJson(Assignment(beforeAssignments, "O1"), Assignment(afterAssignments, "O1"), "QA-BULK-04 leaves O1 semantically unchanged.");
    TestAssertions.EqualDocumentContent(beforeMaps, fixture.ReadJson("maps.json"), "QA-BULK-04 leaves map content unchanged apart from transaction revision metadata.");
    Xunit.Assert.Single(fixture.Store.GetEvents()["events"]?.AsArray() ?? new JsonArray());
    Xunit.Assert.Single(fixture.Store.GetBackups()["backups"]?.AsArray() ?? new JsonArray());
    Console.WriteLine($"QA-BULK-04 changed=[{string.Join(',', changed)}] unchanged=[{string.Join(',', unchanged)}] history=1 backup=1");

    fixture.Store.UndoLastChange(new JsonObject());
    var undoneAssignments = fixture.ReadJson("assignments.json");
    var undoneBySeat = QaBulk04AssignmentSnapshots(undoneAssignments, selected);
    Xunit.Assert.Equal(selected, selected.Where(id => beforeBySeat[id] == undoneBySeat[id]));
    TestAssertions.EqualDocumentContent(beforeAssignments, undoneAssignments, "QA-BULK-04 Undo restores the exact assignment document content.");
    TestAssertions.EqualDocumentContent(beforeMaps, fixture.ReadJson("maps.json"), "QA-BULK-04 Undo restores map content.");
    Console.WriteLine("QA-BULK-04 undo-restored=[F1,F2,F3] undo-unchanged=[R1,O1]");
}

static void VerifyQaBulk04ConcurrentRealityChangeIsAtomic()
{
    using var fixture = new Fixture();
    WriteQaBulk04Reality(fixture);
    var previewEligible = new[] { "F1", "F2", "F3" };
    Console.WriteLine($"QA-BULK-04 concurrency preview-free=[{string.Join(',', previewEligible)}]");

    var assignments = fixture.ReadJson("assignments.json");
    assignments["assignments"]!.AsArray().Add(new JsonObject
    {
        ["workstationId"] = "F2", ["status"] = "confirmed", ["personId"] = "concurrent-person", ["notes"] = "controlled-race"
    });
    fixture.WriteRaw("assignments.json", assignments.ToJsonString());
    var beforeAttempt = fixture.DataHashes();

    var error = Capture(() => fixture.Store.BulkUpdateAssignments(new JsonObject
    {
        ["workstationIds"] = new JsonArray(previewEligible.Select(id => (JsonNode?)id).ToArray()),
        ["status"] = "reserved"
    }));
    var afterAttempt = fixture.DataHashes();

    Xunit.Assert.True(error.Contains("ocupado", StringComparison.OrdinalIgnoreCase), "QA-BULK-04 rejects all three IDs when F2 became Occupied after preview.");
    TestAssertions.EqualHashes(beforeAttempt, afterAttempt, "QA-BULK-04 concurrent rejection leaves every Reality file hash unchanged.");
    Xunit.Assert.Empty(fixture.Store.GetBackups()["backups"]?.AsArray() ?? new JsonArray());
    Xunit.Assert.Empty(fixture.Store.GetEvents()["events"]?.AsArray() ?? new JsonArray());
    Console.WriteLine($"QA-BULK-04 concurrency sent=[{string.Join(',', previewEligible)}] rejected=all hashes-before=[{FormatHashes(beforeAttempt)}] hashes-after=[{FormatHashes(afterAttempt)}]");
}

static void WriteQaBulk04Reality(Fixture fixture)
{
    fixture.WriteRaw("maps.json", new JsonObject
    {
        ["maps"] = new JsonArray(new JsonObject
        {
            ["id"] = "qa-bulk-04", ["name"] = "QA-BULK-04", ["seats"] = new JsonArray(
                new JsonObject { ["id"] = "F1", ["x"] = .1, ["y"] = .1 },
                new JsonObject { ["id"] = "F2", ["x"] = .2, ["y"] = .1 },
                new JsonObject { ["id"] = "R1", ["x"] = .3, ["y"] = .1 },
                new JsonObject { ["id"] = "O1", ["x"] = .4, ["y"] = .1 },
                new JsonObject { ["id"] = "F3", ["x"] = .5, ["y"] = .1 })
        })
    }.ToJsonString());
    fixture.WriteRaw("assignments.json", new JsonObject
    {
        ["version"] = 4,
        ["assignments"] = new JsonArray(
            new JsonObject { ["workstationId"] = "R1", ["status"] = "reserved", ["notes"] = "must-stay-reserved" },
            new JsonObject { ["workstationId"] = "O1", ["status"] = "confirmed", ["personId"] = "occupied-person", ["notes"] = "must-stay-occupied" })
    }.ToJsonString());
}

static IReadOnlyDictionary<string, string> QaBulk04AssignmentSnapshots(JsonObject assignments, IEnumerable<string> workstationIds)
{
    var byId = assignments["assignments"]?.AsArray().OfType<JsonObject>()
        .ToDictionary(item => Text(item["workstationId"]), item => item.ToJsonString(), StringComparer.Ordinal) ?? [];
    return workstationIds.ToDictionary(id => id, id => byId.GetValueOrDefault(id, "<missing>"), StringComparer.Ordinal);
}

static string FormatHashes(IReadOnlyDictionary<string, string> hashes) => string.Join(",", hashes.Select(pair => $"{pair.Key}:{pair.Value}"));

[Fact]
    public void ExportCreatesValidOoxmlWorkbook()
{
    using var fixture = new Fixture();
    var templateRoseta = FirstTemplateRoseta();
    Directory.CreateDirectory(fixture.ExportDirectory);
    fixture.Store.SaveAssignment(new JsonObject { ["workstationId"] = "N-01", ["personId"] = "person-1", ["roseta"] = templateRoseta, ["status"] = "confirmed" }, false);
    var result = fixture.Store.ExportExcel(fixture.ExportDirectory);
    var path = Text(result["path"]);

    Xunit.Assert.True(File.Exists(path), "Export returns a file that exists.");
    Xunit.Assert.True(new FileInfo(path).Length > 0, "The exported workbook is non-empty.");
    Xunit.Assert.True((result["rosetasFromPlan"]?.GetValue<int>() ?? 0) > 0, "The export reports the fixture occupancy from the plan.");

    using var archive = ZipFile.OpenRead(path);
    var names = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.Ordinal);
    foreach (var required in new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml", "xl/worksheets/sheet3.xml" })
        Xunit.Assert.True(names.Contains(required), $"The OOXML package contains {required}.");
    foreach (var entry in archive.Entries.Where(entry => entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
    {
        using var stream = entry.Open();
        _ = XDocument.Load(stream);
    }

    var workbook = ReadXml(archive, "xl/workbook.xml");
    Xunit.Assert.True(workbook.Descendants().Any(element => element.Name.LocalName == "sheet"), "Workbook declares worksheets.");
    var relationships = ReadXml(archive, "xl/_rels/workbook.xml.rels");
    Xunit.Assert.True(relationships.Descendants().Count(element => element.Name.LocalName == "Relationship") >= 3, "Workbook relationships include the worksheets.");
    if (names.Contains("xl/sharedStrings.xml")) _ = ReadXml(archive, "xl/sharedStrings.xml");
    var renderedText = string.Join("\n", new[] { "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml", "xl/worksheets/sheet3.xml" }.Select(name => ReadXml(archive, name).ToString()));
    Xunit.Assert.True(renderedText.Contains("person-1", StringComparison.Ordinal), "The exported sheets contain the expected fixture occupancy.");
    Xunit.Assert.True(fixture.ReadLogs().All(log => !log.Contains(fixture.Root, StringComparison.OrdinalIgnoreCase)), "Export audit logs do not expose the temporary absolute path.");
}

[Fact]
    public void InvalidJsonLoadPreservesFixtureData() => FailedLoadPreservesFixtureData("{ invalid json", deleteMaps: false, "Invalid JSON is rejected.");

[Fact]
    public void TruncatedJsonLoadPreservesFixtureData() => FailedLoadPreservesFixtureData("{\"maps\":[", deleteMaps: false, "Truncated JSON is rejected.");

[Fact]
    public void MissingMandatoryJsonLoadPreservesFixtureData() => FailedLoadPreservesFixtureData(null, deleteMaps: true, "A missing mandatory maps.json file is rejected.");

[Fact]
    public void FailedLoadCannotSaveEmptyDataset()
{
    using var fixture = new Fixture();
    fixture.WriteRaw("maps.json", "{ invalid json");
    var before = fixture.DataHashes();
    _ = Capture(() => fixture.Store.Load());
    var saveError = Capture(() => fixture.Store.SaveAssignment(new JsonObject { ["workstationId"] = "N-02", ["personId"] = "person-3", ["status"] = "confirmed" }, false));
    Xunit.Assert.True(saveError.Length > 0, "A save after a failed load is rejected.");
    TestAssertions.EqualHashes(before, fixture.DataHashes(), "A failed load cannot publish an empty or fallback dataset.");
}

[Fact]
    public void ExclusiveFileLockRejectsDirectSave()
{
    using var fixture = new Fixture();
    var before = fixture.DataHashes();
    var lockPath = Path.Combine(fixture.DataDirectory, ".lock");
    using var holder = StartLockHolder(lockPath);
    try
    {
        Xunit.Assert.Equal("LOCKED", holder.StandardOutput.ReadLine());
        var error = Capture(() => fixture.Store.SaveAssignment(new JsonObject { ["workstationId"] = "N-02", ["personId"] = "person-3", ["status"] = "confirmed" }, false));
        Xunit.Assert.True(error.Contains("No se pudo adquirir el bloqueo de datos", StringComparison.Ordinal), "An exclusive data lock rejects the direct save after the bounded retry.");
        TestAssertions.EqualHashes(before, fixture.DataHashes(), "A rejected locked save leaves all data files unchanged.");
        Xunit.Assert.True(!Directory.EnumerateFiles(fixture.DataDirectory, "*.tmp").Any(), "A rejected locked save publishes no transaction temporary file.");
    }
    finally
    {
        if (!holder.HasExited) holder.Kill(true);
        holder.WaitForExit();
    }
}

static Process StartLockHolder(string lockPath)
{
    var harness = Assembly.GetExecutingAssembly().Location;
    return Process.Start(new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"\"{harness}\" --hold-lock \"{lockPath}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    }) ?? throw new InvalidOperationException("Could not start the isolated lock holder.");
}

static void FailedLoadPreservesFixtureData(string? malformedContent, bool deleteMaps, string expectedMessage)
{
    using var fixture = new Fixture();
    if (deleteMaps) fixture.Delete("maps.json"); else fixture.WriteRaw("maps.json", malformedContent!);
    var before = fixture.DataHashes();
    var error = Capture(() => fixture.Store.Load());
    Xunit.Assert.True(error.Length > 0, expectedMessage);
    TestAssertions.EqualHashes(before, fixture.DataHashes(), "A failed load leaves fixture data byte-for-byte unchanged.");
}

static XDocument ReadXml(ZipArchive archive, string name)
{
    var entry = archive.GetEntry(name) ?? throw new InvalidOperationException($"Missing OOXML entry {name}.");
    using var stream = entry.Open();
    return XDocument.Load(stream);
}

static string FirstTemplateRoseta()
{
    var path = Path.Combine(Environment.CurrentDirectory, "ParcheoCampoTemplate.xlsx");
    using var archive = ZipFile.OpenRead(path);
    var sheet = ReadXml(archive, "xl/worksheets/sheet2.xml");
    var strings = archive.GetEntry("xl/sharedStrings.xml") is { } shared ? ReadSharedStrings(shared) : [];
    var value = sheet.Descendants().Where(element => element.Name.LocalName == "c" && (element.Attribute("r")?.Value?.StartsWith("A", StringComparison.Ordinal) ?? false) && element.Parent?.Attribute("r")?.Value != "1")
        .Select(cell => CellValue(cell, strings)).FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate) && !string.Equals(candidate, "#N/A", StringComparison.OrdinalIgnoreCase) && !string.Equals(candidate, "Roseta", StringComparison.OrdinalIgnoreCase));
    return value ?? throw new InvalidOperationException("The embedded export template has no usable roseta row.");
}

static string[] ReadSharedStrings(ZipArchiveEntry entry)
{
    using var stream = entry.Open();
    var document = XDocument.Load(stream);
    return document.Descendants().Where(element => element.Name.LocalName == "si").Select(item => string.Concat(item.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value))).ToArray();
}

static string CellValue(XElement cell, IReadOnlyList<string> strings)
{
    var value = cell.Descendants().FirstOrDefault(element => element.Name.LocalName == "v")?.Value ?? "";
    return cell.Attribute("t")?.Value == "s" && int.TryParse(value, out var index) && index >= 0 && index < strings.Count ? strings[index] : value;
}


static string AssignmentSeat(JsonObject assignments, string personId) => assignments["assignments"]?.AsArray().OfType<JsonObject>()
    .FirstOrDefault(item => Text(item["personId"]) == personId)?["workstationId"]?.GetValue<string>() ?? "";

static JsonObject Assignment(JsonObject assignments, string workstationId) => assignments["assignments"]?.AsArray().OfType<JsonObject>()
    .Single(item => Text(item["workstationId"]) == workstationId) ?? throw new InvalidOperationException($"Missing assignment for {workstationId}.");

static JsonObject Workspace(JsonObject state, string mapId, string seatId) => state["maps"]?["maps"]?.AsArray().OfType<JsonObject>()
    .Single(map => Text(map["id"]) == mapId)["seats"]?.AsArray().OfType<JsonObject>().Single(seat => Text(seat["id"]) == seatId)
    ?? throw new InvalidOperationException($"Missing workspace {mapId}|{seatId}.");

static JsonObject[] ScenarioChanges(Fixture fixture, string scenarioId) => fixture.Store.GetScenarioDiff(new JsonObject { ["scenarioId"] = scenarioId })["changes"]?.AsArray().OfType<JsonObject>().ToArray()
    ?? throw new InvalidOperationException("Expected scenario changes are missing.");

static JsonObject[] AtomicMembers(IEnumerable<JsonObject> changes, string memberId)
{
    var member = changes.Single(change => Text(change["id"]) == memberId);
    var operationId = Text(member["operationId"]);
    Xunit.Assert.True(operationId.Length > 0, "Atomic movement members expose an operation ID.");
    Xunit.Assert.Equal("movement", Text(member["type"]));
    Xunit.Assert.True(member["atomic"]?.GetValue<bool>() == true, "Atomic movement members expose atomic=true.");
    var members = changes.Where(change => Text(change["operationId"]) == operationId).OrderBy(change => Text(change["id"]), StringComparer.Ordinal).ToArray();
    Xunit.Assert.Equal(2, members.Length);
    return members;
}

static JsonArray ChangeIds(IEnumerable<JsonObject> changes) => new(changes.Select(change => (JsonNode?)Text(change["id"])).ToArray());

static string Capture(Action action)
{
    try { action(); }
    catch (Exception exception) { return exception.Message; }
    throw new InvalidOperationException("Expected operation to fail.");
}

static string Text(JsonNode? node) => node?.GetValue<string>() ?? node?.ToString() ?? "";

sealed class Fixture : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "release-readiness-" + Guid.NewGuid().ToString("N"));
    private readonly string _data;
    internal DataStore Store { get; }

    internal Fixture(bool legacySource = false, bool legacyDevice = true)
    {
        _data = Path.Combine(_root, "data");
        Directory.CreateDirectory(_data);
        Write("maps.json", new JsonObject
        {
            ["maps"] = new JsonArray(
                Map("norte", "Norte",
                    legacySource ? LegacySeat("N-01", .20, .20, "person-1", legacyDevice ? "legacy-device" : null, "legacy-source-roseta") : Seat("N-01", .20, .20),
                    legacySource ? Seat("N-02", .70, .30, "legacy-destination-roseta") : Seat("N-02", .70, .30)),
                Map("sur", "Sur", Seat("S-01", .20, .70), Seat("S-02", .70, .70)))
        });
        Write("assignments.json", new JsonObject { ["version"] = 0, ["assignments"] = legacySource ? new JsonArray() : new JsonArray(Assignment("N-01", "person-1"), Assignment("S-01", "person-2")) });
        Write("positions.json", new JsonObject { ["positions"] = new JsonArray(Position("norte", "N-01", .20, .20), Position("norte", "N-02", .70, .30), Position("sur", "S-01", .20, .70), Position("sur", "S-02", .70, .70)) });
        Write("events.json", new JsonObject { ["events"] = new JsonArray() });
        Write("people.json", new JsonObject { ["people"] = legacySource ? new JsonArray(new JsonObject { ["id"] = "person-1" }) : new JsonArray() });
        Write("devices.json", new JsonObject { ["devices"] = legacySource && legacyDevice ? new JsonArray(new JsonObject { ["id"] = "device-legacy-1", ["name"] = "legacy-device" }) : new JsonArray() });
        Write("locations.json", new JsonObject { ["locations"] = new JsonArray() });
        Write("state.json", new JsonObject { ["revision"] = 0 });
        Store = DataStore.FromConfig(new AppConfig { NetworkRoot = _root, DataFolder = "data", BackupFolder = "backups", LogsFolder = "logs", BackupRetentionMode = "disabled" });
    }

    internal string Root => _root;
    internal string DataDirectory => _data;
    internal string ExportDirectory { get; } = Path.Combine(Path.GetTempPath(), "release-readiness-export-" + Guid.NewGuid().ToString("N"));
    internal string Read(string name) => File.ReadAllText(Path.Combine(_data, name));
    internal JsonObject ReadJson(string name) => JsonNode.Parse(Read(name))?.AsObject() ?? throw new InvalidOperationException($"{name} is not valid JSON.");
    internal void WriteRaw(string name, string content) => File.WriteAllText(Path.Combine(_data, name), content);
    internal void Delete(string name) => File.Delete(Path.Combine(_data, name));
    internal IReadOnlyDictionary<string, string> DataHashes() => Directory.EnumerateFiles(_data, "*.json").OrderBy(path => path, StringComparer.Ordinal).ToDictionary(path => Path.GetFileName(path)!, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);
    internal IReadOnlyList<string> ReadLogs() => Directory.Exists(Path.Combine(_root, "logs")) ? Directory.EnumerateFiles(Path.Combine(_root, "logs"), "*.log").Select(File.ReadAllText).ToArray() : [];
    private void Write(string name, JsonObject value) => File.WriteAllText(Path.Combine(_data, name), value.ToJsonString());
    public void Dispose()
    {
        if (Directory.Exists(ExportDirectory)) Directory.Delete(ExportDirectory, true);
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private static JsonObject Map(string id, string name, params JsonObject[] seats) => new() { ["id"] = id, ["name"] = name, ["seats"] = new JsonArray(seats) };
    private static JsonObject Seat(string id, double x, double y, string? roseta = null)
    {
        var seat = new JsonObject { ["id"] = id, ["x"] = x, ["y"] = y };
        if (roseta is not null) seat["roseta"] = roseta;
        return seat;
    }
    private static JsonObject LegacySeat(string id, double x, double y, string personId, string? deviceName, string roseta)
    {
        var seat = new JsonObject
        {
            ["id"] = id, ["x"] = x, ["y"] = y, ["type"] = "occupied", ["personId"] = personId,
            ["roseta"] = roseta, ["location"] = "legacy-location", ["reference"] = "legacy-reference"
        };
        if (deviceName is not null) seat["deviceName"] = deviceName;
        return seat;
    }
    private static JsonObject Position(string mapId, string seatId, double x, double y) => new() { ["mapId"] = mapId, ["seatId"] = seatId, ["x"] = x, ["y"] = y };
    private static JsonObject Assignment(string workstationId, string personId) => new() { ["workstationId"] = workstationId, ["personId"] = personId, ["status"] = "confirmed" };
}

}
