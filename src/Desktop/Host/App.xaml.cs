namespace PlanoOpenSpaceIT.Windows;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        if (e.Args.Length == 1 && e.Args[0] is "--backup-retention-report" or "--integrity-report")
        {
            try
            {
                var store = DataStore.Create();
                if (e.Args[0] == "--backup-retention-report") store.GetBackupRetentionReport();
                else store.GetIntegrityReport();
                Shutdown(0);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                Shutdown(1);
            }
            return;
        }

        base.OnStartup(e);
    }
}
