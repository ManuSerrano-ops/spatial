using System.Security.Cryptography;
using System.Text.Json.Nodes;
using PlanoOpenSpaceIT.Windows;
using Xunit;

namespace PlanoOpenSpaceIT.Desktop.Tests;

public sealed class ManagedAreasTests
{

[Fact]
    public void LoadCreatePersistenceHistoryAndBackup()
{
    using var fixture = new Fixture();
    Xunit.Assert.True(!File.Exists(fixture.ManagedAreasPath), "The migration fixture starts without managed-areas.json.");
    Xunit.Assert.Empty(Areas(fixture.Store.Load()));

    var created = fixture.Bridge.Dispatch("createManagedArea", new JsonObject
    {
        ["id"] = "north-a", ["mapId"] = "north", ["name"] = "North A", ["workspaceIds"] = Ids("N-2", "N-1")
    }).AsObject();
    Xunit.Assert.True(created["noOp"]?.GetValue<bool>() == false, "Create reports a committed mutation.");
    Xunit.Assert.True(File.Exists(fixture.ManagedAreasPath), "Create persists managed-areas.json.");
    Xunit.Assert.Equal(["N-1", "N-2"], WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")));
    Xunit.Assert.Single(fixture.Store.GetEvents()["events"]?.AsArray() ?? new JsonArray());
    Xunit.Assert.Single(fixture.Store.GetBackups()["backups"]?.AsArray() ?? new JsonArray());

    var reloaded = fixture.NewStore().Load();
    Xunit.Assert.Equal("North A", Text(Area(ManagedDocument(reloaded), "north-a")["name"]));
}

[Fact]
    public void RenameAddAndRemoveCrud()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "North A", "N-1");
    fixture.Store.RenameManagedArea(new JsonObject { ["areaId"] = "north-a", ["name"] = "Finance" });
    Xunit.Assert.Equal("Finance", Text(Area(fixture.ReadManagedAreas(), "north-a")["name"]));

    fixture.Store.AddManagedAreaWorkspaces(new JsonObject { ["areaId"] = "north-a", ["workspaceIds"] = Ids("N-3", "N-2", "N-2") });
    Xunit.Assert.Equal(["N-1", "N-2", "N-3"], WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")));

    fixture.Store.RemoveManagedAreaWorkspaces(new JsonObject { ["areaId"] = "north-a", ["workspaceIds"] = Ids("N-2") });
    Xunit.Assert.Equal(["N-1", "N-3"], WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")));

    var hashes = fixture.DataHashes();
    var backups = fixture.BackupCount;
    var noOp = fixture.Store.RemoveManagedAreaWorkspaces(new JsonObject { ["areaId"] = "north-a", ["workspaceIds"] = Ids("N-4") });
    Xunit.Assert.True(noOp["noOp"]?.GetValue<bool>() == true, "Removing a non-member is a no-op.");
    TestAssertions.EqualHashes(hashes, fixture.DataHashes(), "A no-op writes no data file.");
    Xunit.Assert.Equal(backups, fixture.BackupCount);
}

[Fact]
    public void MembershipAndValidationAreAtomic()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "North A", "N-1");
    fixture.Create("south-a", "south", "South A", "N-1");
    Xunit.Assert.Equal(2, Areas(fixture.Store.Load()).Count);

    TestAssertions.AssertRejectedWithoutWrites(fixture.DataHashes, () => fixture.BackupCount, () => fixture.Create("north-b", "north", "North B", "N-1"), "más de una Managed Area");
    TestAssertions.AssertRejectedWithoutWrites(fixture.DataHashes, () => fixture.BackupCount, () => fixture.Create("missing-map", "missing", "Missing", "N-2"), "no existe");
    TestAssertions.AssertRejectedWithoutWrites(fixture.DataHashes, () => fixture.BackupCount, () => fixture.Create("missing-workspace", "north", "Missing", "N-99"), "no existe");
}

[Fact]
    public void MoveIsAtomic()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "North A", "N-1", "N-2");
    fixture.Create("north-b", "north", "North B", "N-3");
    fixture.Store.MoveManagedAreaWorkspaces(new JsonObject { ["fromAreaId"] = "north-a", ["toAreaId"] = "north-b", ["workspaceIds"] = Ids("N-2", "N-1") });
    Xunit.Assert.Empty(WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")));
    Xunit.Assert.Equal(["N-1", "N-2", "N-3"], WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-b")));

    TestAssertions.AssertRejectedWithoutWrites(fixture.DataHashes, () => fixture.BackupCount, () => fixture.Store.MoveManagedAreaWorkspaces(new JsonObject { ["fromAreaId"] = "north-a", ["toAreaId"] = "north-b", ["workspaceIds"] = Ids("N-4") }), "no pertenece");
    fixture.Create("south-a", "south", "South A", "S-1");
    TestAssertions.AssertRejectedWithoutWrites(fixture.DataHashes, () => fixture.BackupCount, () => fixture.Store.MoveManagedAreaWorkspaces(new JsonObject { ["fromAreaId"] = "north-b", ["toAreaId"] = "south-a", ["workspaceIds"] = Ids("N-1") }), "planos distintos");
}

[Fact]
    public void MergeRetainsTargetIdentity()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "Target", "N-1");
    fixture.Create("north-b", "north", "Source B", "N-2");
    fixture.Create("north-c", "north", "Source C", "N-3");
    fixture.Store.MergeManagedAreas(new JsonObject { ["targetAreaId"] = "north-a", ["sourceAreaIds"] = Ids("north-c", "north-b") });
    var document = fixture.ReadManagedAreas();
    Xunit.Assert.Single(Areas(document));
    Xunit.Assert.Equal("Target", Text(Area(document, "north-a")["name"]));
    Xunit.Assert.Equal(["N-1", "N-2", "N-3"], WorkspaceIds(Area(document, "north-a")));
}

[Fact]
    public void DissolveReleasesMemberships()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "North A", "N-1", "N-2");
    var workspaceDocuments = fixture.FileHashes("maps.json", "assignments.json", "positions.json");
    var selected = new HashSet<string> { "N-1" };
    fixture.Store.DissolveManagedArea(new JsonObject { ["areaId"] = "north-a" });
    Xunit.Assert.Empty(Areas(fixture.ReadManagedAreas()));
    TestAssertions.EqualHashes(workspaceDocuments, fixture.FileHashes("maps.json", "assignments.json", "positions.json"), "Dissolve must not change workspaces, assignments, or coordinates.");
    Xunit.Assert.True(selected.SetEquals(new[] { "N-1" }), "Dissolve does not alter manual selection state.");
    Xunit.Assert.True(fixture.Store.GetEvents()["events"]?.AsArray().Last()?["title"]?.ToString() == "Cluster disuelto", "Dissolve creates one human history event.");
    fixture.Store.UndoLastChange(new JsonObject());
    Xunit.Assert.Equal(["N-1", "N-2"], WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")));
}

[Fact]
    public void CreateWorkspaceInAreaIsAtomicAndUndoable()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "Finance", "N-1");
    var beforeBackups = fixture.BackupCount;
    var created = fixture.Bridge.Dispatch("createSeat", new JsonObject { ["mapId"] = "north", ["x"] = .23, ["y"] = .71, ["targetManagedAreaId"] = "north-a" }).AsObject();
    var workspaceId = Text(created["id"]);
    Xunit.Assert.True(workspaceId.Length > 0, "Create returns a workspace id.");
    Xunit.Assert.Equal("north-a", Text(created["targetManagedAreaId"]));
    Xunit.Assert.True(WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")).Contains(workspaceId), "New workspace belongs to the requested area.");
    var createdSeat = fixture.Store.Load()["maps"]!.AsObject()["maps"]!.AsArray().OfType<JsonObject>().Single(map => Text(map["id"]) == "north")["seats"]!.AsArray().OfType<JsonObject>().Single(seat => Text(seat["id"]) == workspaceId);
    Xunit.Assert.Equal(.23, createdSeat["x"]!.GetValue<double>());
    Xunit.Assert.Equal(.71, createdSeat["y"]!.GetValue<double>());
    Xunit.Assert.Equal(beforeBackups + 1, fixture.BackupCount);
    Xunit.Assert.Equal("Puesto creado en zona", fixture.Store.GetEvents()["events"]?.AsArray().Last()?["title"]?.ToString());

    fixture.Store.UndoLastChange(new JsonObject());
    var afterUndo = fixture.Store.Load();
    Xunit.Assert.True(!afterUndo["maps"]!.AsObject()["maps"]!.AsArray().OfType<JsonObject>().SelectMany(map => map["seats"]!.AsArray().OfType<JsonObject>()).Any(seat => Text(seat["id"]) == workspaceId), "Undo removes the workspace and membership together.");
    Xunit.Assert.Equal(["N-1"], WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")));

    var hashes = fixture.DataHashes(); var backups = fixture.BackupCount;
    TestAssertions.AssertRejectedWithoutWrites(fixture.DataHashes, () => fixture.BackupCount, () => fixture.Bridge.Dispatch("createSeat", new JsonObject { ["mapId"] = "north", ["x"] = .4, ["y"] = .4, ["targetManagedAreaId"] = "missing" }), "ya no existe");
    TestAssertions.EqualHashes(hashes, fixture.DataHashes(), "Missing target area creates neither workspace nor membership.");
    Xunit.Assert.Equal(backups, fixture.BackupCount);
}

[Fact]
    public void ManualClusterCreationMovesConflictingMembershipsAtomically()
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
    Xunit.Assert.Equal(["N-1"], WorkspaceIds(Area(document, "north-a")));
    Xunit.Assert.Equal(["N-2", "N-3"], WorkspaceIds(Area(document, "north-b")));
    Xunit.Assert.Equal(beforeBackups + 1, fixture.BackupCount);
    Xunit.Assert.Equal("Cluster creado", fixture.Store.GetEvents()["events"]?.AsArray().Last()?["title"]?.ToString());

    fixture.Store.UndoLastChange(new JsonObject());
    document = fixture.ReadManagedAreas();
    Xunit.Assert.Equal(["N-1", "N-2"], WorkspaceIds(Area(document, "north-a")));
    Xunit.Assert.True(!Areas(document).OfType<JsonObject>().Any(area => Text(area["id"]) == "north-b"), "Undo removes the newly created cluster.");

    TestAssertions.AssertRejectedWithoutWrites(fixture.DataHashes, () => fixture.BackupCount, () => fixture.Store.CreateManagedArea(new JsonObject
    {
        ["id"] = "duplicate", ["mapId"] = "north", ["name"] = " finance ", ["workspaceIds"] = Ids("N-3")
    }), "ya existe un cluster");
}

[Fact]
    public void DeleteAndMoveTransfersMemberships()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "Delete", "N-1", "N-2");
    fixture.Create("north-b", "north", "Keep", "N-3");
    fixture.Store.DeleteManagedAreaAndMoveWorkspaces(new JsonObject { ["sourceAreaId"] = "north-a", ["targetAreaId"] = "north-b" });
    var document = fixture.ReadManagedAreas();
    Xunit.Assert.Single(Areas(document));
    Xunit.Assert.Equal(["N-1", "N-2", "N-3"], WorkspaceIds(Area(document, "north-b")));
}

[Fact]
    public void UndoUsesRealBackupMechanism()
{
    using var fixture = new Fixture();
    fixture.Create("north-a", "north", "North A", "N-1");
    fixture.Store.AddManagedAreaWorkspaces(new JsonObject { ["areaId"] = "north-a", ["workspaceIds"] = Ids("N-2") });
    Xunit.Assert.Equal(2, WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")).Length);
    Xunit.Assert.Equal("real", Text(fixture.Store.GetUndoPreview(new JsonObject())["scope"]));

    fixture.Store.UndoLastChange(new JsonObject());
    Xunit.Assert.Equal(["N-1"], WorkspaceIds(Area(fixture.ReadManagedAreas(), "north-a")));

    using var initialFixture = new Fixture();
    initialFixture.Create("first", "north", "First", "N-1");
    initialFixture.Store.UndoLastChange(new JsonObject());
    Xunit.Assert.Empty(Areas(initialFixture.ReadManagedAreas()));
}

[Fact]
    public void BridgeExposesEveryAction()
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
    Xunit.Assert.Empty(Areas(fixture.ReadManagedAreas()));
}


static JsonObject ManagedDocument(JsonObject package) => package["managedAreas"]?.AsObject() ?? throw new InvalidOperationException("Load did not include managedAreas.");
static JsonArray Areas(JsonObject packageOrDocument) => (packageOrDocument["managedAreas"] as JsonObject ?? packageOrDocument)["areas"]?.AsArray() ?? throw new InvalidOperationException("Managed Areas document has no areas array.");
static JsonObject Area(JsonObject document, string id) => Areas(document).OfType<JsonObject>().Single(area => Text(area["id"]) == id);
static string[] WorkspaceIds(JsonObject area) => area["workspaceIds"]?.AsArray().Select(Text).ToArray() ?? [];
static JsonArray Ids(params string[] ids) => new(ids.Select(id => (JsonNode?)id).ToArray());
static string Text(JsonNode? node) => node?.GetValue<string>() ?? node?.ToString() ?? "";

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

}
