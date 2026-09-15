using NotificationBarrage.Domain;

namespace NotificationBarrage.Tests;

public sealed class OverlayViewportTests
{
    [Fact]
    public void ViewportContainsOnlyTheBarrageBand()
    {
        var settings = new AppSettings { FontSize = 24, Position = BarragePosition.Bottom };
        var geometry = OverlayLayout.Calculate(1152, settings);

        var viewport = OverlayViewport.Calculate(2048, 1152, geometry);

        Assert.Equal(2048, viewport.Width);
        Assert.True(viewport.Height < 1152);
        Assert.Equal(geometry.LaneHeight * geometry.LaneCount + 16, viewport.Height);
        Assert.True(viewport.Top >= 0);
        Assert.True(viewport.Top + viewport.Height <= 1152);
        Assert.True(viewport.BandTop >= 0);
        Assert.True(viewport.BandTop + geometry.LaneHeight * geometry.LaneCount <= viewport.Height);
    }
}
