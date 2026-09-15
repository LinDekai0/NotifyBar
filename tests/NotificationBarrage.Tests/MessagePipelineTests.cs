using NotificationBarrage.Domain;
using NotificationBarrage.Services;

namespace NotificationBarrage.Tests;

public sealed class MessagePipelineTests
{
    [Fact]
    public void SnapshotDoesNotReplayOldNotificationsOnStartupOrReconnect()
    {
        var tracker = new NotificationSnapshotTracker();
        var old = NotificationMapper.Map(1, "QQ", "QQ", ["好友", "旧消息"], DateTimeOffset.UnixEpoch);
        var fresh = NotificationMapper.Map(2, "QQ", "QQ", ["好友", "新消息"], DateTimeOffset.UnixEpoch.AddSeconds(1));
        Assert.Empty(tracker.Update([old]));
        Assert.Equal(fresh, Assert.Single(tracker.Update([old, fresh])));
        Assert.Empty(tracker.Update([old, fresh]));
        tracker.Reset();
        Assert.Empty(tracker.Update([old, fresh]));
    }

    [Fact]
    public void LayoutKeepsEveryLaneInsidePrimaryDisplay()
    {
        foreach (var position in Enum.GetValues<BarragePosition>())
        foreach (var size in new[] { 12, 24, 72 })
        foreach (var offset in new[] { -500, 0, 500 })
        {
            var layout = OverlayLayout.Calculate(720, new() { Position = position, FontSize = size, VerticalOffset = offset });
            Assert.InRange(layout.BandTop, 0, 720);
            Assert.True(layout.BandTop + layout.LaneHeight * layout.LaneCount <= 720);
        }
    }
}
