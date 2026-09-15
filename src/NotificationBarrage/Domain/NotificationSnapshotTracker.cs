namespace NotificationBarrage.Domain;

/// <summary>Seeds a notification-center snapshot and emits only subsequently added identities.</summary>
public sealed class NotificationSnapshotTracker
{
    private HashSet<Guid> _previous = new();
    private bool _seeded;
    public void Reset() { _previous.Clear(); _seeded = false; }
    public IReadOnlyList<IncomingNotification> Update(IEnumerable<IncomingNotification> snapshot)
    {
        var entries = snapshot.OrderBy(n => n.ReceivedAt).ToArray();
        var added = _seeded ? entries.Where(n => !_previous.Contains(n.Id)).DistinctBy(n => n.Id).ToArray() : [];
        _previous = entries.Select(n => n.Id).ToHashSet();
        _seeded = true;
        return added;
    }
}
