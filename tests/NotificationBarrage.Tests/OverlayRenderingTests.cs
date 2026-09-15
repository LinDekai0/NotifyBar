using NotificationBarrage.Domain;
using NotificationBarrage.UI;

namespace NotificationBarrage.Tests;

public sealed class OverlayRenderingTests
{
    [Fact]
    public void BarrageItemUsesCompositorFriendlyRendering()
    {
        var error = default(Exception);
        var thread = new Thread(() =>
        {
            try
            {
                var item = new BarrageItemControl(
                    new BarrageMessage("test.app", "Test", "Title", "Body", DateTimeOffset.UtcNow, "test-id"),
                    new AppSettings(), 640);
                Assert.Null(item.Effect);
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error is not null) throw error;
    }

    [Fact]
    public void OverlayHasNoPeriodicTopmostRefreshThatCanStallAnimation()
    {
        Assert.DoesNotContain(typeof(OverlayWindow).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), field => field.Name == "_topmost");
    }
}
