namespace NotificationBarrage.Domain;

public sealed record OverlayViewport(double Top, double Height, double BandTop, double Width)
{
    public static OverlayViewport Calculate(double screenWidth, double screenHeight, OverlayGeometry geometry, double padding = 8)
    {
        var bandHeight = geometry.LaneHeight * geometry.LaneCount;
        var viewportHeight = Math.Min(screenHeight, bandHeight + padding * 2);
        var top = Math.Clamp(geometry.BandTop - padding, 0, Math.Max(0, screenHeight - viewportHeight));
        var bandTop = geometry.BandTop - top;
        return new(top, viewportHeight, bandTop, screenWidth);
    }
}
