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

    [Theory]
    [InlineData("QQ", true)] [InlineData("微信", true)] [InlineData("WeChat", true)]
    [InlineData("NotQQ", false)] [InlineData("WeChatBackup", false)]
    public void MatchesOnlySupportedSources(string name, bool expected)
    {
        var incoming = new IncomingNotification(Guid.NewGuid(), name, name, "好友", "消息", DateTimeOffset.Now);
        Assert.Equal(expected, new MessageFilter().TryCreate(incoming, new(), out _));
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
