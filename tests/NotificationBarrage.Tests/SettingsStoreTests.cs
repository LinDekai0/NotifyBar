using NotificationBarrage.Domain;
using NotificationBarrage.Services;

namespace NotificationBarrage.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void LoadMissingFileReturnsDefaults()
    {
        using var directory = new TemporaryDirectory();
        var settings = new SettingsStore(directory.Path).Load();
        Assert.True(settings.EnableQQ);
        Assert.Equal(BarragePosition.Bottom, settings.Position);
        Assert.Equal(300, settings.SpeedPixelsPerSecond);
    }

    [Fact]
    public void SaveAndLoadRoundTripsSettings()
    {
        using var directory = new TemporaryDirectory();
        var expected = new AppSettings { EnableQQ = false, Position = BarragePosition.Bottom, Opacity = 0.4, MaxBodyLength = 250 };
        var store = new SettingsStore(directory.Path);
        store.Save(expected);
        var actual = store.Load();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LoadCorruptJsonBacksItUpAndReturnsDefaults()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(Path.Combine(directory.Path, "settings.json"), "{broken");
        var settings = new SettingsStore(directory.Path).Load();
        Assert.Equal(new AppSettings(), settings);
        Assert.Single(Directory.GetFiles(directory.Path, "settings.json.broken-*"));
    }

    [Fact]
    public void SaveClampsUnsafeValues()
    {
        using var directory = new TemporaryDirectory();
        var store = new SettingsStore(directory.Path);
        store.Save(new AppSettings { SpeedPixelsPerSecond = 1, FontSize = 100, Opacity = 0, MaxBodyLength = 1, VerticalOffset = 900 });
        var settings = store.Load();
        Assert.Equal(50, settings.SpeedPixelsPerSecond);
        Assert.Equal(72, settings.FontSize);
        Assert.Equal(0.2, settings.Opacity);
        Assert.Equal(20, settings.MaxBodyLength);
        Assert.Equal(500, settings.VerticalOffset);
    }

    [Fact]
    public void NormalizeReplacesNonFiniteFloatingPointValues()
    {
        var settings = new AppSettings { FontSize = double.NaN, Opacity = double.PositiveInfinity }.Normalize();
        Assert.Equal(24, settings.FontSize);
        Assert.Equal(0.92, settings.Opacity);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NotificationBarrageTests", Guid.NewGuid().ToString("N"));
        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }
}
