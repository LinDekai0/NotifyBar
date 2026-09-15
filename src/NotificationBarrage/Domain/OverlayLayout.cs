namespace NotificationBarrage.Domain;

public sealed record OverlayGeometry(double BandTop, double LaneHeight, int LaneCount);

public static class OverlayLayout
{
    public static OverlayGeometry Calculate(double screenHeight, AppSettings settings)
    {
        var row = Math.Ceiling(settings.Normalize().FontSize * 1.5 + 22);
        var lanes = Math.Max(1, Math.Min(5, (int)(screenHeight / row)));
        var height = row * lanes;
        var y = settings.Position switch
        {
            BarragePosition.Top => screenHeight * .07,
            BarragePosition.Center => (screenHeight - height) / 2,
            _ => screenHeight * .85 - height / 2
        };
        return new(Math.Clamp(y + settings.VerticalOffset, 0, Math.Max(0, screenHeight - height)), row, lanes);
    }
}
