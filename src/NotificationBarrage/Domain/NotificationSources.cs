namespace NotificationBarrage.Domain;

public enum LegacyNotificationSource
{
    QQ,
    WeChat
}

public sealed record NotificationSourceInfo(string AppId, string DisplayName)
{
    internal static NotificationSourceInfo[] Normalize(IEnumerable<NotificationSourceInfo> sources)
    {
        var byId = new Dictionary<string, NotificationSourceInfo>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (source is null || string.IsNullOrWhiteSpace(source.AppId)) continue;
            var appId = source.AppId.Trim();
            var displayName = string.IsNullOrWhiteSpace(source.DisplayName) ? appId : source.DisplayName.Trim();
            byId[appId] = new NotificationSourceInfo(appId, displayName);
        }
        return Sort(byId.Values);
    }

    internal static NotificationSourceInfo[] Sort(IEnumerable<NotificationSourceInfo> sources) => sources
        .OrderBy(source => source.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(source => source.AppId, StringComparer.Ordinal)
        .ToArray();
}

internal static class LegacyNotificationSourceMatcher
{
    private static readonly string[] QQNames = ["QQ", "腾讯QQ", "腾讯 QQ", "QQ NT", "QQNT", "Tencent.QQ"];
    private static readonly string[] WeChatNames = ["微信", "WeChat", "Weixin", "腾讯微信", "Tencent.WeChat", "Tencent.Weixin"];

    public static bool TryMatch(NotificationSourceInfo source, out LegacyNotificationSource legacy)
    {
        if (Matches(source, QQNames))
        {
            legacy = LegacyNotificationSource.QQ;
            return true;
        }
        if (Matches(source, WeChatNames))
        {
            legacy = LegacyNotificationSource.WeChat;
            return true;
        }
        legacy = default;
        return false;
    }

    private static bool Matches(NotificationSourceInfo source, IEnumerable<string> names)
    {
        if (names.Any(name => string.Equals(source.AppId, name, StringComparison.OrdinalIgnoreCase))) return true;
        return names.Where(name => name.StartsWith("Tencent.", StringComparison.Ordinal)).Any(name =>
            source.AppId.StartsWith(name + "_", StringComparison.OrdinalIgnoreCase)
            || source.AppId.StartsWith(name + "!", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class NotificationSourceCatalog
{
    private readonly object _sync = new();
    private readonly Dictionary<string, NotificationSourceInfo> _sources = new(StringComparer.Ordinal);

    public NotificationSourceCatalog(IEnumerable<NotificationSourceInfo>? initialSources = null)
    {
        foreach (var source in NotificationSourceInfo.Normalize(initialSources ?? []))
            _sources[source.AppId] = source;
    }

    public IReadOnlyList<NotificationSourceInfo> Snapshot
    {
        get { lock (_sync) return NotificationSourceInfo.Sort(_sources.Values); }
    }

    public bool Update(IEnumerable<IncomingNotification> notifications)
    {
        var changed = false;
        lock (_sync)
        {
            foreach (var notification in notifications)
            {
                if (string.IsNullOrWhiteSpace(notification.AppId)) continue;
                var appId = notification.AppId.Trim();
                var displayName = string.IsNullOrWhiteSpace(notification.AppName) ? appId : notification.AppName.Trim();
                var source = new NotificationSourceInfo(appId, displayName);
                if (_sources.TryGetValue(appId, out var existing) && existing == source) continue;
                _sources[appId] = source;
                changed = true;
            }
        }
        return changed;
    }
}

public sealed class NotificationSourcesChangedEventArgs(IReadOnlyList<NotificationSourceInfo> sources) : EventArgs
{
    public IReadOnlyList<NotificationSourceInfo> Sources { get; } = sources;
}

public sealed class NotificationSnapshotProcessor
{
    public long AccessRevision { get; private set; }
    public void ObserveAccess(NotificationAccessStatus access)
    {
        if (access == NotificationAccessStatus.Allowed) return;
        AccessRevision++;
        _tracker.Reset();
    }
    private readonly NotificationSourceCatalog _catalog = new();
    private readonly NotificationSnapshotTracker _tracker = new();

    public event EventHandler<NotificationSourcesChangedEventArgs>? SourcesChanged;
    public IReadOnlyList<NotificationSourceInfo> Sources => _catalog.Snapshot;

    public IReadOnlyList<IncomingNotification> Update(IEnumerable<IncomingNotification> snapshot)
    {
        var entries = snapshot.OrderBy(notification => notification.ReceivedAt).ToArray();
        if (_catalog.Update(entries))
            SourcesChanged?.Invoke(this, new NotificationSourcesChangedEventArgs(_catalog.Snapshot));
        return _tracker.Update(entries);
    }

    public void Reset() => _tracker.Reset();
}

public sealed class SourceSelectionDraft
{
    private readonly Dictionary<string, NotificationSourceInfo> _sources = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);

    public SourceSelectionDraft(IEnumerable<NotificationSourceInfo> sources, IEnumerable<string> enabledSourceIds)
    {
        foreach (var source in NotificationSourceInfo.Normalize(sources)) _sources[source.AppId] = source;
        foreach (var appId in enabledSourceIds.Where(id => !string.IsNullOrWhiteSpace(id))) _selected.Add(appId.Trim());
    }

    public IReadOnlyList<NotificationSourceInfo> Sources => NotificationSourceInfo.Sort(_sources.Values);
    public IReadOnlyList<string> SelectedSourceIds => _selected.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    public int Revision { get; private set; }
    public bool IsEnabled(string appId) => _selected.Contains(appId);

    public void SetEnabled(string appId, bool enabled)
    {
        if (!_sources.ContainsKey(appId)) return;
        var changed = enabled ? _selected.Add(appId) : _selected.Remove(appId);
        if (changed) Revision++;
    }

    public void SetAll(IEnumerable<string> appIds, bool enabled)
    {
        foreach (var appId in appIds) SetEnabled(appId, enabled);
    }

    public void MergeSources(IEnumerable<NotificationSourceInfo> sources, IEnumerable<string> enabledDefaultsForNewSources)
    {
        var enabledDefaults = enabledDefaultsForNewSources.ToHashSet(StringComparer.Ordinal);
        foreach (var source in NotificationSourceInfo.Normalize(sources))
        {
            var isNew = !_sources.ContainsKey(source.AppId);
            var changed = isNew || _sources[source.AppId] != source;
            _sources[source.AppId] = source;
            if (isNew && enabledDefaults.Contains(source.AppId)) changed |= _selected.Add(source.AppId);
            if (changed) Revision++;
        }
    }
}
