using System.Threading;

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
        }

        return 0;
    }
}
