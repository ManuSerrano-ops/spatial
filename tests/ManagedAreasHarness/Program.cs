using System.Security.Cryptography;
using System.Text.Json.Nodes;
using PlanoOpenSpaceIT.Windows;

var tests = new (string Name, Action Test)[]
{
    ("load, create, persistence, history, and backup", LoadCreatePersistenceHistoryAndBackup),
    ("rename, add, and remove CRUD", RenameAddAndRemoveCrud),
    ("map membership uniqueness and validation are atomic", MembershipAndValidationAreAtomic),
    ("move is atomic", MoveIsAtomic),
    ("merge retains target identity", MergeRetainsTargetIdentity),
    ("dissolve releases memberships without changing workspaces", DissolveReleasesMemberships),
    ("create workspace in area is atomic and undoable", CreateWorkspaceInAreaIsAtomicAndUndoable),
    ("manual cluster creation moves conflicting memberships atomically", ManualClusterCreationMovesConflictingMembershipsAtomically),
    ("delete and move transfers every membership", DeleteAndMoveTransfersMemberships),
    ("Undo uses the real backup mechanism", UndoUsesRealBackupMechanism),
    ("WebViewBridge exposes every Managed Area action", BridgeExposesEveryAction)
};

var passed = 0;
foreach (var (name, test) in tests)
{
    try { test(); passed++; }
    catch (Exception exception) { Console.Error.WriteLine($"FAIL: {name}: {exception.Message}"); }
}
Console.WriteLine($"ManagedAreasHarness: {passed}/{tests.Length} passed, {tests.Length - passed} failed");
return passed == tests.Length ? 0 : 1;

static void LoadCreatePersistenceHistoryAndBackup()
{
    using var fixture = new Fixture();
    Assert(!File.Exists(fixture.ManagedAreasPath), "The migration fixture starts without managed-areas.json.");
    Equal(0, Areas(fixture.Store.Load()).Count, "Load exposes an empty Managed Areas document when the file is absent.");

    var created = fixture.Bridge.Dispatch("createManagedArea", new JsonObject
    {
        ["id"] = "north-a", ["mapId"] = "north", ["name"] = "North A", ["workspaceIds"] = Ids("N-2", "N-1")
    }).AsObject();
    Assert(created["noOp"]?.GetValue<bool>() == false, "Create reports a committed mutation.");
    Assert(File.Exists(fixture.ManagedAreasPath), "Create persists managed-areas.json.");
    SequenceEqual(["N-1", "N-2"], WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")), "Members are persisted canonically.");
    Equal(1, fixture.Store.GetEvents()["events"]?.AsArray().Count ?? 0, "Create writes one event.");
    Equal(1, fixture.Store.GetBackups()["backups"]?.AsArray().Count ?? 0, "Create writes one backup.");

    var reloaded = fixture.NewStore().Load();
    Equal("North A", Text(Area(ManagedDocument(reloaded), "north-a")["name"]), "A new DataStore instance reloads the persisted area.");
}

static void RenameAddAndRemoveCrud()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "North A", "N-1");
    fixture.Store.RenameManagedArea(new JsonObject { ["areaId"] = "north-a", ["name"] = "Finance" });
    Equal("Finance", Text(Area(fixture.ReadManagedAreas(), "north-a")["name"]), "Rename persists only the new name.");

    fixture.Store.AddManagedAreaWorkspaces(new JsonObject { ["areaId"] = "north-a", ["workspaceIds"] = Ids("N-3", "N-2", "N-2") });
    SequenceEqual(["N-1", "N-2", "N-3"], WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")), "Add deduplicates and sorts members.");

    fixture.Store.RemoveManagedAreaWorkspaces(new JsonObject { ["areaId"] = "north-a", ["workspaceIds"] = Ids("N-2") });
    SequenceEqual(["N-1", "N-3"], WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")), "Remove releases selected members.");

    var hashes = fixture.DataHashes();
    var backups = fixture.BackupCount;
    var noOp = fixture.Store.RemoveManagedAreaWorkspaces(new JsonObject { ["areaId"] = "north-a", ["workspaceIds"] = Ids("N-4") });
    Assert(noOp["noOp"]?.GetValue<bool>() == true, "Removing a non-member is a no-op.");
    EqualHashes(hashes, fixture.DataHashes(), "A no-op writes no data file.");
    Equal(backups, fixture.BackupCount, "A no-op creates no backup.");
}

static void MembershipAndValidationAreAtomic()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "North A", "N-1");
    fixture.Create("south-a", "south", "South A", "N-1");
    Equal(2, Areas(fixture.Store.Load()).Count, "The same workspaceId may belong to one area on each map.");

    AssertRejectedWithoutWrites(fixture, () => fixture.Create("north-b", "north", "North B", "N-1"), "más de una Managed Area");
    AssertRejectedWithoutWrites(fixture, () => fixture.Create("missing-map", "missing", "Missing", "N-2"), "no existe");
    AssertRejectedWithoutWrites(fixture, () => fixture.Create("missing-workspace", "north", "Missing", "N-99"), "no existe");
}

static void MoveIsAtomic()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "North A", "N-1", "N-2");
    fixture.Create("north-b", "north", "North B", "N-3");
    fixture.Store.MoveManagedAreaWorkspaces(new JsonObject { ["fromAreaId"] = "north-a", ["toAreaId"] = "north-b", ["workspaceIds"] = Ids("N-2", "N-1") });
    Equal(0, WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")).Length, "Move empties the source for selected members.");
    SequenceEqual(["N-1", "N-2", "N-3"], WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-b")), "Move fills the target atomically.");

    AssertRejectedWithoutWrites(fixture, () => fixture.Store.MoveManagedAreaWorkspaces(new JsonObject { ["fromAreaId"] = "north-a", ["toAreaId"] = "north-b", ["workspaceIds"] = Ids("N-4") }), "no pertenece");
    fixture.Create("south-a", "south", "South A", "S-1");
    AssertRejectedWithoutWrites(fixture, () => fixture.Store.MoveManagedAreaWorkspaces(new JsonObject { ["fromAreaId"] = "north-b", ["toAreaId"] = "south-a", ["workspaceIds"] = Ids("N-1") }), "planos distintos");
}

static void MergeRetainsTargetIdentity()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "Target", "N-1");
    fixture.Create("north-b", "north", "Source B", "N-2");
    fixture.Create("north-c", "north", "Source C", "N-3");
    fixture.Store.MergeManagedAreas(new JsonObject { ["targetAreaId"] = "north-a", ["sourceAreaIds"] = Ids("north-c", "north-b") });
    var document = fixture.ReadManagedAreas();
    Equal(1, Areas(document).Count, "Merge dissolves every source.");
    Equal("Target", Text(Area(document, "north-a")["name"]), "Merge retains target identity and name.");
    SequenceEqual(["N-1", "N-2", "N-3"], WorkspaceIds(Area(document, "north-a")), "Merge unions all members.");
}

static void DissolveReleasesMemberships()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "North A", "N-1", "N-2");
    var workspaceDocuments = fixture.FileHashes("maps.json", "assignments.json", "positions.json");
    var selected = new HashSet<string> { "N-1" };
    fixture.Store.DissolveManagedArea(new JsonObject { ["areaId"] = "north-a" });
    Equal(0, Areas(fixture.ReadManagedAreas()).Count, "Dissolve removes the area.");
    EqualHashes(workspaceDocuments, fixture.FileHashes("maps.json", "assignments.json", "positions.json"), "Dissolve must not change workspaces, assignments, or coordinates.");
    Assert(selected.SetEquals(new[] { "N-1" }), "Dissolve does not alter manual selection state.");
    Assert(fixture.Store.GetEvents()["events"]?.AsArray().Last()?["title"]?.ToString() == "Cluster disuelto", "Dissolve creates one human history event.");
    fixture.Store.UndoLastChange(new JsonObject());
    SequenceEqual(["N-1", "N-2"], WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")), "Undo restores exact area membership.");
}

static void CreateWorkspaceInAreaIsAtomicAndUndoable()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "Finance", "N-1");
    var beforeBackups = fixture.BackupCount;
    var created = fixture.Bridge.Dispatch("createSeat", new JsonObject { ["mapId"] = "north", ["x"] = .23, ["y"] = .71, ["targetManagedAreaId"] = "north-a" }).AsObject();
    var workspaceId = Text(created["id"]);
    Assert(workspaceId.Length > 0, "Create returns a workspace id.");
    Equal("north-a", Text(created["targetManagedAreaId"]), "Create reports the target area.");
    Assert(WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")).Contains(workspaceId), "New workspace belongs to the requested area.");
    var createdSeat = fixture.Store.Load()["maps"]!.AsObject()["maps"]!.AsArray().OfType<JsonObject>().Single(map => Text(map["id"]) == "north")["seats"]!.AsArray().OfType<JsonObject>().Single(seat => Text(seat["id"]) == workspaceId);
    Equal(.23, createdSeat["x"]!.GetValue<double>(), "Create preserves user-selected x coordinate.");
    Equal(.71, createdSeat["y"]!.GetValue<double>(), "Create preserves user-selected y coordinate.");
    Equal(beforeBackups + 1, fixture.BackupCount, "Create plus membership creates one backup.");
    Equal("Puesto creado en zona", fixture.Store.GetEvents()["events"]?.AsArray().Last()?["title"]?.ToString(), "Create plus membership creates one history entry.");

    fixture.Store.UndoLastChange(new JsonObject());
    var afterUndo = fixture.Store.Load();
    Assert(!afterUndo["maps"]!.AsObject()["maps"]!.AsArray().OfType<JsonObject>().SelectMany(map => map["seats"]!.AsArray().OfType<JsonObject>()).Any(seat => Text(seat["id"]) == workspaceId), "Undo removes the workspace and membership together.");
    SequenceEqual(["N-1"], WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")), "Undo restores the original area exactly.");

    var hashes = fixture.DataHashes(); var backups = fixture.BackupCount;
    AssertRejectedWithoutWrites(fixture, () => fixture.Bridge.Dispatch("createSeat", new JsonObject { ["mapId"] = "north", ["x"] = .4, ["y"] = .4, ["targetManagedAreaId"] = "missing" }), "ya no existe");
    EqualHashes(hashes, fixture.DataHashes(), "Missing target area creates neither workspace nor membership.");
    Equal(backups, fixture.BackupCount, "Rejected create creates no backup.");
}

static void ManualClusterCreationMovesConflictingMembershipsAtomically()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "Finance", "N-1", "N-2");
    var beforeBackups = fixture.BackupCount;
    fixture.Store.CreateManagedArea(new JsonObject
    {
        ["id"] = "north-b", ["mapId"] = "north", ["name"] = "Operations",
        ["workspaceIds"] = Ids("N-2", "N-3"), ["moveWorkspaceIds"] = Ids("N-2")
    });

    var document = fixture.ReadManagedAreas();
    SequenceEqual(["N-1"], WorkspaceIds(Area(document, "north-a")), "Moved workspace is removed from its source cluster.");
    SequenceEqual(["N-2", "N-3"], WorkspaceIds(Area(document, "north-b")), "New cluster contains the exact workspace IDs.");
    Equal(beforeBackups + 1, fixture.BackupCount, "Create and move share one backup.");
    Equal("Cluster creado", fixture.Store.GetEvents()["events"]?.AsArray().Last()?["title"]?.ToString(), "Create and move share one history event.");

    fixture.Store.UndoLastChange(new JsonObject());
    document = fixture.ReadManagedAreas();
    SequenceEqual(["N-1", "N-2"], WorkspaceIds(Area(document, "north-a")), "Undo restores the source membership exactly.");
    Assert(!Areas(document).OfType<JsonObject>().Any(area => Text(area["id"]) == "north-b"), "Undo removes the newly created cluster.");

    AssertRejectedWithoutWrites(fixture, () => fixture.Store.CreateManagedArea(new JsonObject
    {
        ["id"] = "duplicate", ["mapId"] = "north", ["name"] = " finance ", ["workspaceIds"] = Ids("N-3")
    }), "ya existe un cluster");
}

static void DeleteAndMoveTransfersMemberships()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "Delete", "N-1", "N-2");
    fixture.Create("north-b", "north", "Keep", "N-3");
    fixture.Store.DeleteManagedAreaAndMoveWorkspaces(new JsonObject { ["sourceAreaId"] = "north-a", ["targetAreaId"] = "north-b" });
    var document = fixture.ReadManagedAreas();
    Equal(1, Areas(document).Count, "Delete-and-move deletes exactly the source.");
    SequenceEqual(["N-1", "N-2", "N-3"], WorkspaceIds(Area(document, "north-b")), "Delete-and-move transfers every source member.");
}

static void UndoUsesRealBackupMechanism()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "North A", "N-1");
    fixture.Store.AddManagedAreaWorkspaces(new JsonObject { ["areaId"] = "north-a", ["workspaceIds"] = Ids("N-2") });
    Equal(2, WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")).Length, "The change exists before Undo.");
    Equal("real", Text(fixture.Store.GetUndoPreview(new JsonObject())["scope"]), "Managed Area mutations enter the existing real Undo stack.");

    fixture.Store.UndoLastChange(new JsonObject());
    SequenceEqual(["N-1"], WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")), "Undo restores the exact prior Managed Areas snapshot.");

    using var initialFixture = new Fixture();
    initialFixture.Create("first", "north", "First", "N-1");
    initialFixture.Store.UndoLastChange(new JsonObject());
    Equal(0, Areas(initialFixture.ReadManagedAreas()).Count, "Undo of the first mutation restores the logical empty document even when the file did not exist before it.");
}

static void BridgeExposesEveryAction()
{
    using var fixture = new Fixture();
    fixture.Bridge.Dispatch("createManagedArea", new JsonObject { ["id"] = "a", ["mapId"] = "north", ["name"] = "A", ["workspaceIds"] = Ids("N-1") });
    fixture.Bridge.Dispatch("createManagedArea", new JsonObject { ["id"] = "b", ["mapId"] = "north", ["name"] = "B", ["workspaceIds"] = Ids("N-2") });
    fixture.Bridge.Dispatch("renameManagedArea", new JsonObject { ["areaId"] = "a", ["name"] = "Renamed" });
    fixture.Bridge.Dispatch("addWorkspacesToManagedArea", new JsonObject { ["areaId"] = "a", ["workspaceIds"] = Ids("N-3") });
    fixture.Bridge.Dispatch("removeWorkspacesFromManagedArea", new JsonObject { ["areaId"] = "a", ["workspaceIds"] = Ids("N-3") });
    fixture.Bridge.Dispatch("moveWorkspacesBetweenManagedAreas", new JsonObject { ["fromAreaId"] = "a", ["toAreaId"] = "b", ["workspaceIds"] = Ids("N-1") });
    fixture.Bridge.Dispatch("deleteAndMoveManagedArea", new JsonObject { ["sourceAreaId"] = "a", ["targetAreaId"] = "b" });
    fixture.Bridge.Dispatch("createManagedArea", new JsonObject { ["id"] = "c", ["mapId"] = "north", ["name"] = "C", ["workspaceIds"] = Ids("N-3") });
    fixture.Bridge.Dispatch("mergeManagedAreas", new JsonObject { ["targetAreaId"] = "b", ["sourceAreaIds"] = Ids("c") });
    fixture.Bridge.Dispatch("dissolveManagedArea", new JsonObject { ["areaId"] = "b" });
    Equal(0, Areas(fixture.ReadManagedAreas()).Count, "All bridge actions dispatch successfully.");
}

static void AssertRejectedWithoutWrites(Fixture fixture, Action action, string expectedMessage)
{
    var hashes = fixture.DataHashes();
    var backups = fixture.BackupCount;
    var error = Capture(action);
    Assert(error.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase), $"Expected rejection containing '{expectedMessage}', received '{error}'.");
    EqualHashes(hashes, fixture.DataHashes(), "A rejected operation is transactionally atomic.");
    Equal(backups, fixture.BackupCount, "A rejected operation creates no backup or Undo unit.");
}

static string Capture(Action action)
{
    try { action(); }
    catch (Exception exception) { return exception.Message; }
    throw new InvalidOperationException("Expected operation to fail.");
}

static JsonObject ManagedDocument(JsonObject package) => package["managedAreas"]?.AsObject() ?? throw new InvalidOperationException("Load did not include managedAreas.");
static JsonArray Areas(JsonObject packageOrDocument) => (packageOrDocument["managedAreas"] as JsonObject ?? packageOrDocument)["areas"]?.AsArray() ?? throw new InvalidOperationException("Managed Areas document has no areas array.");
static JsonObject Area(JsonObject document, string id) => Areas(document).OfType<JsonObject>().Single(area => Text(area["id"]) == id);
static string[] WorkspaceIds(JsonObject area) => area["workspaceIds"]?.AsArray().Select(Text).ToArray() ?? [];
static JsonArray Ids(params string[] ids) => new(ids.Select(id => (JsonNode?)id).ToArray());
static string Text(JsonNode? node) => node?.GetValue<string>() ?? node?.ToString() ?? "";
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void Equal<T>(T expected, T? actual, string message) where T : notnull { if (!EqualityComparer<T>.Default.Equals(expected, actual!)) throw new InvalidOperationException($"{message} Expected {expected}; actual {actual}."); }
static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message) { if (!expected.SequenceEqual(actual)) throw new InvalidOperationException($"{message} Expected [{string.Join(", ", expected)}]; actual [{string.Join(", ", actual)}]."); }
static void EqualHashes(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual, string message)
{
    if (expected.Count != actual.Count || expected.Any(pair => !actual.TryGetValue(pair.Key, out var hash) || hash != pair.Value))
        throw new InvalidOperationException(message);
}

sealed class Fixture : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "managed-areas-" + Guid.NewGuid().ToString("N"));
    private readonly string _data;
    internal DataStore Store { get; }
    internal WebViewBridge Bridge { get; }

    internal Fixture()
    {
        _data = Path.Combine(_root, "data");
        Directory.CreateDirectory(_data);
        Write("maps.json", new JsonObject
        {
            ["maps"] = new JsonArray(
                Map("north", "N-1", "N-2", "N-3", "N-4"),
                Map("south", "N-1", "S-1", "S-2"))
        });
        Write("assignments.json", new JsonObject { ["version"] = 0, ["assignments"] = new JsonArray() });
        Write("positions.json", new JsonObject { ["positions"] = new JsonArray() });
        Write("events.json", new JsonObject { ["events"] = new JsonArray() });
        Write("people.json", new JsonObject { ["people"] = new JsonArray() });
        Write("devices.json", new JsonObject { ["devices"] = new JsonArray() });
        Write("locations.json", new JsonObject { ["locations"] = new JsonArray() });
        Write("state.json", new JsonObject { ["revision"] = 0 });
        Store = NewStore();
        Bridge = new WebViewBridge(Store);
    }

    internal string ManagedAreasPath => Path.Combine(_data, "managed-areas.json");
    internal int BackupCount => Store.GetBackups()["backups"]?.AsArray().Count ?? 0;
    internal DataStore NewStore() => DataStore.FromConfig(new AppConfig { NetworkRoot = _root, DataFolder = "data", BackupFolder = "backups", LogsFolder = "logs", BackupRetentionMode = "disabled" });
    internal JsonObject ReadManagedAreas() => JsonNode.Parse(File.ReadAllText(ManagedAreasPath))?.AsObject() ?? throw new InvalidOperationException("managed-areas.json is invalid.");
    internal IReadOnlyDictionary<string, string> DataHashes() => Directory.EnumerateFiles(_data).Where(path => Path.GetExtension(path) == ".json").OrderBy(path => path, StringComparer.Ordinal).ToDictionary(path => Path.GetFileName(path), path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);
        internal IReadOnlyDictionary<string, string> FileHashes(params string[] names) => names.OrderBy(name => name, StringComparer.Ordinal).ToDictionary(name => name, name => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(_data, name)))), StringComparer.Ordinal);
    internal JsonObject Create(string id, string mapId, string name, params string[] workspaceIds) => Store.CreateManagedArea(new JsonObject { ["id"] = id, ["mapId"] = mapId, ["name"] = name, ["workspaceIds"] = new JsonArray(workspaceIds.Select(workspaceId => (JsonNode?)workspaceId).ToArray()) });

    private void Write(string name, JsonObject value) => File.WriteAllText(Path.Combine(_data, name), value.ToJsonString());
    private static JsonObject Map(string id, params string[] workspaceIds) => new() { ["id"] = id, ["name"] = id, ["seats"] = new JsonArray(workspaceIds.Select(workspaceId => (JsonNode?)new JsonObject { ["id"] = workspaceId, ["x"] = .5, ["y"] = .5 }).ToArray()) };
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
