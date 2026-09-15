namespace NotificationBarrage.Domain;

public enum BarragePosition
{
    Top,
    Center,
    Bottom
}

public sealed record AppSettings
{
    public bool EnableQQ { get; init; } = true;
    public bool EnableWeChat { get; init; } = true;
    public BarragePosition Position { get; init; } = BarragePosition.Bottom;
    public int VerticalOffset { get; init; } = 0;
    public int SpeedPixelsPerSecond { get; init; } = 300;
    public double FontSize { get; init; } = 24;
    public double Opacity { get; init; } = 0.92;
    public int MaxBodyLength { get; init; } = 120;
    public bool StartWithWindows { get; init; }

    public AppSettings Normalize() => this with
    {
        VerticalOffset = Math.Clamp(VerticalOffset, -500, 500),
        SpeedPixelsPerSecond = Math.Clamp(SpeedPixelsPerSecond, 50, 2000),
        FontSize = ClampFinite(FontSize, 12, 72, 24),
        Opacity = ClampFinite(Opacity, 0.2, 1.0, 0.92),
        MaxBodyLength = Math.Clamp(MaxBodyLength, 20, 500),
        Position = Enum.IsDefined(Position) ? Position : BarragePosition.Bottom
    };

    private static double ClampFinite(double value, double minimum, double maximum, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
