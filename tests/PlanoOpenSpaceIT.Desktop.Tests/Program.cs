using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using PlanoOpenSpaceIT.Windows;

namespace PlanoOpenSpaceIT.Desktop.Tests;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args is ["--export-visual-fixture", var outputDirectory])
        {
            return ExportVisualFixtures(outputDirectory);
        }

        if (args is ["--hold-lock", var lockPath])
        {
            using var heldLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            Console.WriteLine("LOCKED");
            Console.Out.Flush();
            Thread.Sleep(TimeSpan.FromSeconds(30));
            return 0;
        }

        return args is ["--commit-then-die", var root] ? CommitThenDie(root) : 0;
    }

    private static int ExportVisualFixtures(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        Directory.CreateDirectory(outputDirectory);

        ExportVisualFixture(outputDirectory, "general", CreateGeneralFixture);
        ExportVisualFixture(outputDirectory, "sur-denso", CreateDenseSouthFixture);
        return 0;
    }

    private static void ExportVisualFixture(string outputDirectory, string fixtureName, Func<JsonObject> createMaps)
    {
        var root = Path.Combine(Path.GetTempPath(), $"plano-visual-fixture-{fixtureName}-{Guid.NewGuid():N}");
        var data = Path.Combine(root, "data");
        try
        {
            Directory.CreateDirectory(data);
            var scenarioId = WriteFixtureDocuments(data, createMaps(), includeScenario: fixtureName == "general");

            var store = DataStore.FromConfig(new AppConfig
            {
                NetworkRoot = root,
                DataFolder = "data",
                BackupFolder = "backups",
                LogsFolder = "logs",
                BackupRetentionMode = "disabled"
            });
            var bridge = new WebViewBridge(store);
            var fixture = new JsonObject
            {
                ["loadInitialDataResult"] = Canonicalize(bridge.Dispatch("loadInitialData", new JsonObject())),
                ["runValidationResult"] = Canonicalize(bridge.Dispatch("runValidation", new JsonObject())),
                ["runSpatialAnalyticsResult"] = Canonicalize(bridge.Dispatch("runSpatialAnalytics", new JsonObject()))
            };
            if (scenarioId is not null)
            {
                var scenarioPayload = new JsonObject { ["scenarioId"] = scenarioId };
                fixture["loadScenarioDataResult"] = Canonicalize(bridge.Dispatch("loadInitialData", scenarioPayload));
                fixture["runScenarioValidationResult"] = Canonicalize(bridge.Dispatch("runValidation", scenarioPayload));
                fixture["runScenarioSpatialAnalyticsResult"] = Canonicalize(bridge.Dispatch("runSpatialAnalytics", scenarioPayload));
                fixture["getScenarioDiffResult"] = Canonicalize(bridge.Dispatch("getScenarioDiff", scenarioPayload));
            }

            var path = Path.Combine(outputDirectory, fixtureName + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(fixture, FixtureJsonOptions));
            Console.WriteLine(path);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string? WriteFixtureDocuments(string data, JsonObject maps, bool includeScenario)
    {
        var assignments = FixtureAssignments();
        WriteFixtureDocument(data, "maps.json", maps);
        WriteFixtureDocument(data, "assignments.json", assignments);
        WriteFixtureDocument(data, "positions.json", Positions(maps));
        WriteFixtureDocument(data, "events.json", new JsonObject { ["events"] = new JsonArray() });
        WriteFixtureDocument(data, "people.json", new JsonObject
        {
            ["people"] = new JsonArray(
                new JsonObject { ["id"] = "fixture-person-ada", ["name"] = "Ada Fixture" },
                new JsonObject { ["id"] = "fixture-person-bea", ["name"] = "Bea Fixture" })
        });
        WriteFixtureDocument(data, "devices.json", new JsonObject
        {
            ["devices"] = new JsonArray(
                new JsonObject { ["id"] = "fixture-device-ada", ["name"] = "Equipo Ada" },
                new JsonObject { ["id"] = "fixture-device-bea", ["name"] = "Equipo Bea" })
        });
        WriteFixtureDocument(data, "locations.json", new JsonObject
        {
            ["locations"] = new JsonArray(
                new JsonObject { ["id"] = "fixture-location-norte", ["name"] = "Norte" },
                new JsonObject { ["id"] = "fixture-location-sur", ["name"] = "Sur" })
        });
        WriteFixtureDocument(data, "state.json", new JsonObject { ["revision"] = 0 });
        if (!includeScenario) return null;

        WriteFixtureDocument(data, "scenarios.json", CreateGeneralDiffScenario(maps, assignments));
        return GeneralDiffScenarioId;
    }

    private static JsonObject FixtureAssignments() => new()
    {
        ["version"] = 0,
        ["assignments"] = new JsonArray(
            Assignment("G-01", "fixture-person-ada", "fixture-device-ada", "fixture-location-norte", "R-G-01"),
            Assignment("S-D01", "fixture-person-bea", "fixture-device-bea", "fixture-location-sur", "R-S-D01"),
            new JsonObject { ["workstationId"] = "S-D03", ["status"] = "reserved" })
    };

    private static JsonObject CreateGeneralFixture() => new()
    {
        ["maps"] = new JsonArray(
            Map("norte", "Norte", Seat("G-01", .18, .24), Seat("G-02", .49, .43), Seat("G-03", .77, .68)),
            Map("sur", "Sur", Seat("S-D01", .26, .31), Seat("S-D02", .58, .62), Seat("S-D03", .78, .40)))
    };

    private static JsonObject CreateGeneralDiffScenario(JsonObject maps, JsonObject assignments)
    {
        // Static input keeps the visual diff reproducible without API-generated IDs or timestamps.
        var baseState = ScenarioState(maps, assignments);
        var draft = (JsonObject)baseState.DeepClone();
        var draftMaps = draft["maps"]?.AsObject() ?? throw new InvalidOperationException("El borrador no contiene planos.");
        var north = draftMaps["maps"]?.AsArray().OfType<JsonObject>().Single(map => map["id"]?.GetValue<string>() == "norte")
            ?? throw new InvalidOperationException("El borrador no contiene Norte.");
        var south = draftMaps["maps"]?.AsArray().OfType<JsonObject>().Single(map => map["id"]?.GetValue<string>() == "sur")
            ?? throw new InvalidOperationException("El borrador no contiene Sur.");
        var northSeats = north["seats"]?.AsArray() ?? throw new InvalidOperationException("Norte no contiene puestos.");
        northSeats.OfType<JsonObject>().Single(seat => seat["id"]?.GetValue<string>() == "G-02")["x"] = .57;
        northSeats.OfType<JsonObject>().Single(seat => seat["id"]?.GetValue<string>() == "G-03")["roseta"] = "R-G-03";
        foreach (var seat in Enumerable.Range(1, 60).Select(index => (JsonNode?)Seat($"G-DIFF-{index:D2}", .08 + (index % 6) * .14, .12 + (index / 6) * .02))) northSeats.Add(seat);
        south["seats"]?.AsArray()?.RemoveAt(1);

        var draftAssignments = draft["assignments"]?["assignments"]?.AsArray() ?? throw new InvalidOperationException("El borrador no contiene asignaciones.");
        var g01 = draftAssignments.OfType<JsonObject>().Single(assignment => assignment["workstationId"]?.GetValue<string>() == "G-01");
        g01["locationId"] = "fixture-location-sur";
        g01["roseta"] = "R-G-01-MOVIDA";
        draftAssignments.RemoveAt(1);
        draftAssignments.Add(Assignment("G-02", "fixture-person-bea", "fixture-device-bea", "fixture-location-norte", "R-G-02"));

        return new JsonObject
        {
            ["scenarios"] = new JsonArray(new JsonObject
            {
                ["id"] = GeneralDiffScenarioId,
                ["name"] = "Diff visual general",
                ["baseRevision"] = 0,
                ["base"] = baseState,
                ["draft"] = draft,
                ["operations"] = new JsonArray(),
                ["undo"] = new JsonArray()
            })
        };
    }

    private static JsonObject ScenarioState(JsonObject maps, JsonObject assignments) => new()
    {
        ["maps"] = maps.DeepClone(),
        ["assignments"] = assignments.DeepClone(),
        ["positions"] = Positions(maps)["positions"]?.DeepClone(),
        ["version"] = assignments["version"]?.DeepClone()
    };

    private const string GeneralDiffScenarioId = "fixture-general-diff";

    private static JsonObject CreateDenseSouthFixture()
    {
        // 7.39px on the Sur SVG's 1122.6667px-wide viewBox, expressed as normalized coordinates.
        const double surWidthPixels = 1122.6667;
        const double surHeightPixels = 793.33331;
        const double minimumSeparationPixels = 7.39;
        var horizontalSeparation = minimumSeparationPixels / surWidthPixels;
        var verticalSeparation = minimumSeparationPixels / surHeightPixels;

        return new JsonObject
        {
            ["maps"] = new JsonArray(
                Map("sur", "Sur",
                    Seat("S-D01", .320000, .410000),
                    Seat("S-D02", .320000 + horizontalSeparation, .410000),
                    Seat("S-D03", .470000, .520000),
                    Seat("S-D04", .470000, .520000 + verticalSeparation),
                    Seat("S-D05", .625000, .355000),
                    Seat("S-D06", .625000 + horizontalSeparation, .355000 + verticalSeparation)),
                Map("norte", "Norte", Seat("G-01", .15, .21), Seat("G-02", .72, .73)))
        };
    }

    private static JsonObject Map(string id, string name, params JsonObject[] seats) => new()
    {
        ["id"] = id,
        ["name"] = name,
        ["image"] = id == "sur" ? "plano_sur_limpio.svg" : "plano_norte_limpio.svg",
        ["seats"] = new JsonArray(seats)
    };

    private static JsonObject Seat(string id, double x, double y) => new()
    {
        ["id"] = id,
        ["x"] = x,
        ["y"] = y
    };

    private static JsonObject Assignment(string workstationId, string personId, string deviceId, string locationId, string roseta) => new()
    {
        ["workstationId"] = workstationId,
        ["personId"] = personId,
        ["deviceId"] = deviceId,
        ["locationId"] = locationId,
        ["roseta"] = roseta,
        ["status"] = "confirmed"
    };

    private static JsonObject Positions(JsonObject maps) => new()
    {
        ["positions"] = new JsonArray((maps["maps"]?.AsArray().OfType<JsonObject>() ?? [])
            .SelectMany(map => (map["seats"]?.AsArray().OfType<JsonObject>() ?? []).Select(seat => (JsonNode?)new JsonObject
            {
                ["mapId"] = map["id"]?.DeepClone(),
                ["seatId"] = seat["id"]?.DeepClone(),
                ["x"] = seat["x"]?.DeepClone(),
                ["y"] = seat["y"]?.DeepClone()
            })).ToArray())
    };

    private static void WriteFixtureDocument(string data, string name, JsonObject document) =>
        File.WriteAllText(Path.Combine(data, name), JsonSerializer.Serialize(document, FixtureJsonOptions));

    private static JsonNode? Canonicalize(JsonNode? node)
    {
        return node switch
        {
            JsonObject source => CanonicalizeObject(source),
            JsonArray source => new JsonArray(source.Select(Canonicalize).ToArray()),
            _ => node?.DeepClone()
        };
    }

    private static JsonObject CanonicalizeObject(JsonObject source)
    {
        var canonical = new JsonObject();
        foreach (var property in source.OrderBy(property => property.Key, StringComparer.Ordinal))
        {
            canonical[property.Key] = property.Key == "durationMs" ? 0 : Canonicalize(property.Value);
        }
        return canonical;
    }

    private static readonly JsonSerializerOptions FixtureJsonOptions = new() { WriteIndented = true };

    private static int CommitThenDie(string root)
    {
        var observer = new CommitObserver(Path.Combine(root, "data"), root);
        observer.Start();
        if (!observer.WaitUntilReady(TimeSpan.FromSeconds(5)))
        {
            Console.Error.WriteLine("Observer did not start.");
            return 3;
        }

        var originalPriority = Thread.CurrentThread.Priority;
        try
        {
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            var store = DataStore.FromConfig(new AppConfig { NetworkRoot = root, DataFolder = "data", BackupFolder = "backups", LogsFolder = "logs", BackupRetentionMode = "disabled" });
            store.SaveAssignment(new JsonObject { ["workstationId"] = "S-0002", ["personId"] = "person-committed", ["status"] = "confirmed" }, false);
        }
        finally
        {
            Thread.CurrentThread.Priority = originalPriority;
            observer.Stop();
            observer.Join(TimeSpan.FromSeconds(5));
        }

        if (!observer.Alive)
        {
            Console.Error.WriteLine("Observer did not see transaction activity.");
            return 4;
        }

        WriteMarker(Path.Combine(root, "observer.committed"), "COMMITTED");
        Console.WriteLine("COMMITTED");
        Console.Out.Flush();
        return 0;
    }

    private sealed class CommitObserver
    {
        private readonly string _data;
        private readonly string _aliveMarker;
        private readonly string _windowMarker;
        private readonly DateTime _initialStateWriteTimeUtc;
        private readonly long _initialStateLength;
        private readonly ManualResetEventSlim _ready = new();
        private readonly Thread _thread;
        private int _stop;
        private int _alive;

        internal CommitObserver(string data, string root)
        {
            _data = data;
            _aliveMarker = Path.Combine(root, "observer.alive");
            _windowMarker = Path.Combine(root, "observer.window");
            var state = new FileInfo(Path.Combine(_data, "state.json"));
            _initialStateWriteTimeUtc = state.LastWriteTimeUtc;
            _initialStateLength = state.Length;
            _thread = new Thread(Observe) { IsBackground = true, Priority = ThreadPriority.Highest };
        }

        internal bool Alive => Volatile.Read(ref _alive) == 1;

        internal void Start() => _thread.Start();

        internal bool WaitUntilReady(TimeSpan timeout) => _ready.Wait(timeout);

        internal void Stop() => Volatile.Write(ref _stop, 1);

        internal void Join(TimeSpan timeout)
        {
            if (!_thread.Join(timeout)) throw new InvalidOperationException("Observer did not stop.");
        }

        private void Observe()
        {
            _ready.Set();
            while (Volatile.Read(ref _stop) == 0)
            {
                try
                {
                    var pendingExists = File.Exists(Path.Combine(_data, "commit.pending"));
                    var temporaries = Directory.EnumerateFiles(_data, "*.tmp").Select(Path.GetFileName).ToArray();
                    var hasTemporary = temporaries.Length > 0;
                    var hasTransactionTemporary = temporaries.Any(name => name is not null && Regex.IsMatch(name, @"\.[0-9a-fA-F]{32}\.tmp$"));
                    if (pendingExists || hasTemporary) MarkAlive();
                    if (pendingExists && hasTransactionTemporary && DestinationStateWasPublished())
                    {
                        WriteMarker(_windowMarker, "WINDOW");
                        Console.WriteLine("WINDOW");
                        Console.Out.Flush();
                        using var process = Process.GetCurrentProcess();
                        process.Kill(entireProcessTree: true);
                        Thread.Sleep(Timeout.Infinite);
                    }
                }
                catch (IOException)
                {
                    // An atomic replacement can race with the observation; retry immediately.
                }
                catch (UnauthorizedAccessException)
                {
                    // The fixture owns the directory and can be cleaning after a failed test.
                    return;
                }
                Thread.SpinWait(256);
            }
        }

        private bool DestinationStateWasPublished()
        {
            var state = new FileInfo(Path.Combine(_data, "state.json"));
            return state.Exists && (state.LastWriteTimeUtc != _initialStateWriteTimeUtc || state.Length != _initialStateLength);
        }

        private void MarkAlive()
        {
            if (Interlocked.Exchange(ref _alive, 1) != 0) return;
            WriteMarker(_aliveMarker, "ALIVE");
            Console.WriteLine("ALIVE");
            Console.Out.Flush();
        }

    }

    private static void WriteMarker(string path, string value)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        using var writer = new StreamWriter(stream);
        writer.Write(value);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }
}
