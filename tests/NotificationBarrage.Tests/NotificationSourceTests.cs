using NotificationBarrage.Domain;
using NotificationBarrage.Services;

namespace NotificationBarrage.Tests;

public sealed class NotificationSourceTests
{
    [Fact]
    public void LegacyMigrationIgnoresSameNameUntilRecognizedApplicationIdArrives()
    {
        var settings = new AppSettings { PendingLegacySources = [LegacyNotificationSource.QQ] };
        var unrelated = settings.MergeDiscoveredSources([new("unrelated.app", "QQ")]);
        Assert.False(unrelated.IsSourceEnabled("unrelated.app"));
        Assert.Contains(LegacyNotificationSource.QQ, unrelated.PendingLegacySources);
        var migrated = unrelated.MergeDiscoveredSources([new("Tencent.QQ_abc!App", "Renamed client")]);
        Assert.True(migrated.IsSourceEnabled("Tencent.QQ_abc!App"));
        Assert.False(migrated.IsSourceEnabled("unrelated.app"));
        Assert.Empty(migrated.PendingLegacySources);
    }

    [Fact]
    public void ObservedPermissionLossRequiresFreshBaselineAfterAccessReturns()
    {
        var processor = new NotificationSnapshotProcessor();
        var existing = Incoming("telegram.desktop", "Telegram", "old");
        processor.ObserveAccess(NotificationAccessStatus.Allowed);
        Assert.Empty(processor.Update([existing]));
        var revision = processor.AccessRevision;
        processor.ObserveAccess(NotificationAccessStatus.Denied);
        Assert.NotEqual(revision, processor.AccessRevision);
        processor.ObserveAccess(NotificationAccessStatus.Allowed);
        var duringDenial = Incoming("telegram.desktop", "Telegram", "during denial");
        Assert.Empty(processor.Update([existing, duringDenial]));
        var afterRecovery = Incoming("telegram.desktop", "Telegram", "new");
        Assert.Equal(afterRecovery, Assert.Single(processor.Update([existing, duringDenial, afterRecovery])));
    }

    [Fact]
    public void EnabledApplicationIdPassesButSameNameWithDifferentIdDoesNot()
    {
        var settings = new AppSettings
        {
            KnownSources = [new("telegram.desktop", "Telegram")],
            EnabledSourceIds = ["telegram.desktop"]
        };
        var enabled = Incoming("telegram.desktop", "Telegram", "消息");
        var sameName = Incoming("telegram.preview", "Telegram", "消息");
        var filter = new MessageFilter();

        Assert.True(filter.TryCreate(enabled, settings, out var message));
        Assert.Equal("telegram.desktop", message!.AppId);
        Assert.False(filter.TryCreate(sameName, settings, out _));
    }

    [Fact]
    public void NewlyDiscoveredApplicationIsDisabledByDefault()
    {
        var settings = new AppSettings().MergeDiscoveredSources([new("telegram.desktop", "Telegram")]);

        Assert.Single(settings.KnownSources);
        Assert.False(settings.IsSourceEnabled("telegram.desktop"));
    }

    [Fact]
    public void CatalogCoalescesDuplicateIdsAndUpdatesDisplayName()
    {
        var catalog = new NotificationSourceCatalog();
        catalog.Update([
            Incoming("telegram.desktop", "Telegram", ""),
            Incoming("telegram.desktop", "Telegram Preview", "")
        ]);
        catalog.Update([Incoming("telegram.desktop", "Telegram Desktop", "")]);

        var source = Assert.Single(catalog.Snapshot);
        Assert.Equal("telegram.desktop", source.AppId);
        Assert.Equal("Telegram Desktop", source.DisplayName);
    }

    [Fact]
    public void FirstSnapshotDiscoversEmptyNoticeBeforeSuppressingReplay()
    {
        var processor = new NotificationSnapshotProcessor();
        var discoveredBeforeReturn = false;
        processor.SourcesChanged += (_, _) => discoveredBeforeReturn = true;

        var added = processor.Update([Incoming("telegram.desktop", "Telegram", "", "")]);

        Assert.True(discoveredBeforeReturn);
        Assert.Equal("telegram.desktop", Assert.Single(processor.Sources).AppId);
        Assert.Empty(added);
    }

    [Fact]
    public void RefreshMergeKeepsUnsavedSelectionsAndUsesDefaultsOnlyForNewRows()
    {
        var draft = new SourceSelectionDraft(
            [new("telegram.desktop", "Telegram")],
            ["telegram.desktop"]);
        draft.SetEnabled("telegram.desktop", false);

        draft.MergeSources(
            [new("telegram.desktop", "Telegram Desktop"), new("signal.desktop", "Signal")],
            ["telegram.desktop", "signal.desktop"]);

        Assert.False(draft.IsEnabled("telegram.desktop"));
        Assert.True(draft.IsEnabled("signal.desktop"));
        Assert.Equal("Telegram Desktop", draft.Sources.Single(s => s.AppId == "telegram.desktop").DisplayName);
    }

    [Fact]
    public void SelectionRevisionRevealsChangesMadeWhileSaveIsInFlight()
    {
        var draft = new SourceSelectionDraft([new("telegram.desktop", "Telegram")], []);
        var revisionAtSave = draft.Revision;

        draft.SetEnabled("telegram.desktop", true);

        Assert.NotEqual(revisionAtSave, draft.Revision);
        Assert.True(draft.IsEnabled("telegram.desktop"));
    }

    [Fact]
    public void SaveMergeKeepsSelectionsForSourcesDiscoveredAfterClick()
    {
        var latest = new AppSettings
        {
            KnownSources = [new("telegram.desktop", "Telegram"), new("qq.new", "QQ")],
            EnabledSourceIds = ["qq.new"]
        };
        var clickedDraft = new AppSettings
        {
            KnownSources = [new("telegram.desktop", "Telegram")],
            EnabledSourceIds = ["telegram.desktop"],
            FontSize = 30
        };

        var merged = latest.MergeUserChanges(clickedDraft);

        Assert.True(merged.IsSourceEnabled("telegram.desktop"));
        Assert.True(merged.IsSourceEnabled("qq.new"));
        Assert.Equal(30, merged.FontSize);
    }

    [Fact]
    public void DisabledSourceBacklogIsRejectedAtDisplayTime()
    {
        var settings = new AppSettings
        {
            KnownSources = [new("telegram.desktop", "Telegram")],
            EnabledSourceIds = ["telegram.desktop"]
        };
        var filter = new MessageFilter();
        filter.TryCreate(Incoming("telegram.desktop", "Telegram", "queued"), settings, out var queued);

        var disabled = settings.WithEnabledSourceIds([]);

        Assert.False(BarrageDisplayGate.ShouldDisplay(queued!, disabled));
        Assert.True(BarrageDisplayGate.ShouldDisplay(queued! with { IsTest = true }, disabled));
    }

    [Fact]
    public void SameContentFromDifferentApplicationIdsIsNotDeduplicated()
    {
        var settings = new AppSettings
        {
            KnownSources = [new("one.app", "One"), new("two.app", "Two")],
            EnabledSourceIds = ["one.app", "two.app"]
        };
        var filter = new MessageFilter();
        filter.TryCreate(Incoming("one.app", "Same", "hello"), settings, out var first);
        filter.TryCreate(Incoming("two.app", "Same", "hello"), settings, out var second);
        var queue = new BarrageQueue();

        Assert.True(queue.Enqueue(first!));
        Assert.True(queue.Enqueue(second!));
    }

    private static IncomingNotification Incoming(string appId, string appName, string body, string title = "好友") =>
        new(Guid.NewGuid(), appId, appName, title, body, DateTimeOffset.UtcNow);
}
