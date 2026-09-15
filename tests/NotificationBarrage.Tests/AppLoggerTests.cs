using NotificationBarrage.Services;

namespace NotificationBarrage.Tests;

public sealed class AppLoggerTests
{
    [Fact]
    public void OversizedMetadataDoesNotExceedLogFileCap()
    {
        using var directory = new TemporaryDirectory();
        var logger = new AppLogger(directory.Path);
        logger.Info("large-event", new { Details = new string('x', 3 * 1024 * 1024) });
        var file = Directory.GetFiles(directory.Path, "*.log").Single();
        Assert.True(new FileInfo(file).Length <= 2 * 1024 * 1024);
    }

    [Fact]
    public void ErrorDoesNotWriteExceptionMessage()
    {
        using var directory = new TemporaryDirectory();
        const string secret = "private-notification-body";
        new AppLogger(directory.Path).Error("notification-failed", new InvalidOperationException(secret));
        var log = File.ReadAllText(Directory.GetFiles(directory.Path, "*.log").Single());
        Assert.DoesNotContain(secret, log);
        Assert.Contains("InvalidOperationException", log);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NotificationBarrageLoggerTests", Guid.NewGuid().ToString("N"));
        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }
}
