using System.Diagnostics;
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
