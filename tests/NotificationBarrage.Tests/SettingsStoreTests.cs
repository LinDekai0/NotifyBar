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
        Assert.Empty(settings.EnabledSourceIds);
        Assert.Empty(settings.KnownSources);
        Assert.Empty(settings.PendingLegacySources);
        Assert.Equal(BarragePosition.Bottom, settings.Position);
        Assert.Equal(300, settings.SpeedPixelsPerSecond);
    }

    [Fact]
    public void SaveAndLoadRoundTripsSettings()
    {
        using var directory = new TemporaryDirectory();
        var expected = new AppSettings
        {
            KnownSources = [new("telegram.app", "Telegram")],
            EnabledSourceIds = ["telegram.app"],
            Position = BarragePosition.Bottom,
            Opacity = 0.4,
            MaxBodyLength = 250
        };
        var store = new SettingsStore(directory.Path);
        store.Save(expected);
        var actual = store.Load();
        Assert.Equal(expected.Position, actual.Position);
        Assert.Equal(expected.Opacity, actual.Opacity);
        Assert.Equal(expected.MaxBodyLength, actual.MaxBodyLength);
        Assert.Equal(expected.KnownSources, actual.KnownSources);
        Assert.Equal(expected.EnabledSourceIds, actual.EnabledSourceIds);
    }

    [Fact]
    public void SaveAndLoadPreservesExplicitlyEmptySelection()
    {
        using var directory = new TemporaryDirectory();
        var store = new SettingsStore(directory.Path);
        store.Save(new AppSettings { KnownSources = [new("telegram.app", "Telegram")], EnabledSourceIds = [] });

        var actual = store.Load();

        Assert.False(actual.IsSourceEnabled("telegram.app"));
        Assert.Empty(actual.EnabledSourceIds);
    }

    [Fact]
    public void LoadOldJsonMigratesOnlyEnabledLegacyApplicationsWhenDiscovered()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(Path.Combine(directory.Path, "settings.json"), """
        { "EnableQQ": true, "EnableWeChat": false, "Position": "Bottom" }
        """);
        var store = new SettingsStore(directory.Path);
        var pendingMigration = store.Load();
        store.Save(pendingMigration);

        var migrated = store.Load().MergeDiscoveredSources([
            new("Tencent.QQ_abc!App", "QQ"),
            new("Tencent.WeChat_xyz!App", "微信")
        ]);

        Assert.True(migrated.IsSourceEnabled("Tencent.QQ_abc!App"));
        Assert.False(migrated.IsSourceEnabled("Tencent.WeChat_xyz!App"));
        Assert.Empty(migrated.PendingLegacySources);
    }

    [Fact]
    public void SavedCatalogContainsNoNotificationContent()
    {
        using var directory = new TemporaryDirectory();
        var store = new SettingsStore(directory.Path);
        store.Save(new AppSettings
        {
            KnownSources = [new("telegram.app", "Telegram")],
            EnabledSourceIds = ["telegram.app"]
        });

        var json = File.ReadAllText(Path.Combine(directory.Path, "settings.json"));

        Assert.Contains("telegram.app", json);
        Assert.DoesNotContain("\"Title\":", json);
        Assert.DoesNotContain("\"Body\":", json);
    }

    [Fact]
    public void LoadCorruptJsonBacksItUpAndReturnsDefaults()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(Path.Combine(directory.Path, "settings.json"), "{broken");
        var settings = new SettingsStore(directory.Path).Load();
        Assert.Empty(settings.EnabledSourceIds);
        Assert.Equal(BarragePosition.Bottom, settings.Position);
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
