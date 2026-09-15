namespace NotificationBarrage.Domain;

public enum BarragePosition
{
    Top,
    Center,
    Bottom
}

public sealed record AppSettings
{
    public NotificationSourceInfo[] KnownSources { get; init; } = [];
    public string[] EnabledSourceIds { get; init; } = [];
    public LegacyNotificationSource[] PendingLegacySources { get; init; } = [];
    public BarragePosition Position { get; init; } = BarragePosition.Bottom;
    public int VerticalOffset { get; init; } = 0;
    public int SpeedPixelsPerSecond { get; init; } = 300;
    public double FontSize { get; init; } = 24;
    public double Opacity { get; init; } = 0.92;
    public int MaxBodyLength { get; init; } = 120;
    public bool StartWithWindows { get; init; }

    public bool IsSourceEnabled(string? appId) =>
        !string.IsNullOrWhiteSpace(appId) && EnabledSourceIds.Contains(appId.Trim(), StringComparer.Ordinal);

    public AppSettings WithEnabledSourceIds(IEnumerable<string> appIds) => (this with
    {
        EnabledSourceIds = NormalizeIds(appIds)
    }).Normalize();

    public AppSettings MergeUserChanges(AppSettings userChanges)
    {
        var latest = Normalize();
        var draft = userChanges.Normalize();
        var sourcesVisibleAtSave = draft.KnownSources.Select(source => source.AppId).ToHashSet(StringComparer.Ordinal);
        var enabled = draft.EnabledSourceIds.ToHashSet(StringComparer.Ordinal);
        enabled.UnionWith(latest.EnabledSourceIds.Where(appId => !sourcesVisibleAtSave.Contains(appId)));
        return (draft with
        {
            KnownSources = latest.KnownSources,
            EnabledSourceIds = NormalizeIds(enabled),
            PendingLegacySources = latest.PendingLegacySources
        }).Normalize();
    }

    public AppSettings MergeDiscoveredSources(IEnumerable<NotificationSourceInfo> discoveredSources)
    {
        var normalized = Normalize();
        var known = normalized.KnownSources.ToDictionary(source => source.AppId, StringComparer.Ordinal);
        var enabled = normalized.EnabledSourceIds.ToHashSet(StringComparer.Ordinal);
        var pending = normalized.PendingLegacySources.ToHashSet();

        foreach (var source in NotificationSourceInfo.Normalize(discoveredSources))
        {
            known[source.AppId] = source;
            if (LegacyNotificationSourceMatcher.TryMatch(source, out var legacy) && pending.Remove(legacy))
                enabled.Add(source.AppId);
        }

        return (normalized with
        {
            KnownSources = NotificationSourceInfo.Sort(known.Values),
            EnabledSourceIds = NormalizeIds(enabled),
            PendingLegacySources = pending.OrderBy(value => value).ToArray()
        }).Normalize();
    }

    public AppSettings Normalize()
    {
        var known = NotificationSourceInfo.Normalize(KnownSources ?? []);
        return this with
        {
            KnownSources = known,
            EnabledSourceIds = NormalizeIds(EnabledSourceIds ?? []),
            PendingLegacySources = (PendingLegacySources ?? []).Where(Enum.IsDefined).Distinct().OrderBy(value => value).ToArray(),
            VerticalOffset = Math.Clamp(VerticalOffset, -500, 500),
            SpeedPixelsPerSecond = Math.Clamp(SpeedPixelsPerSecond, 50, 2000),
            FontSize = ClampFinite(FontSize, 12, 72, 24),
            Opacity = ClampFinite(Opacity, 0.2, 1.0, 0.92),
            MaxBodyLength = Math.Clamp(MaxBodyLength, 20, 500),
            Position = Enum.IsDefined(Position) ? Position : BarragePosition.Bottom
        };
    }

    private static string[] NormalizeIds(IEnumerable<string> appIds) => appIds
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Select(id => id.Trim())
        .Distinct(StringComparer.Ordinal)
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToArray();

    private static double ClampFinite(double value, double minimum, double maximum, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
