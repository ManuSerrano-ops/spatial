using PlanoOpenSpaceIT.Windows;
using Xunit;

namespace PlanoOpenSpaceIT.Desktop.Tests;

public sealed class SafeLoggerAvailabilityTests
{
    [Fact]
    public void AuditWarningClearsAfterRecoveryAndReturnsAfterLaterFailure()
    {
        var root = Path.Combine(Path.GetTempPath(), "safe-logger-availability-" + Guid.NewGuid().ToString("N"));
        var logs = Path.Combine(root, "logs");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(logs, "blocked");
            var availability = new List<AuditLogAvailability>();
            var logger = new SafeLogger(logs, availabilityChanged: availability.Add);
            logger.Info("initial.failure");
            logger.Error("initial.failure", new IOException("blocked"));

            Assert.Equal([AuditLogAvailability.Unavailable], availability);

            File.Delete(logs);
            Directory.CreateDirectory(logs);
            logger.Info("recovery.probe");

            Assert.Equal([AuditLogAvailability.Unavailable, AuditLogAvailability.Available], availability);

            Directory.Delete(logs, recursive: true);
            File.WriteAllText(logs, "blocked-again");
            logger.Error("later.failure", new IOException("blocked"));

            Assert.Equal(
                [AuditLogAvailability.Unavailable, AuditLogAvailability.Available, AuditLogAvailability.Unavailable],
                availability);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
