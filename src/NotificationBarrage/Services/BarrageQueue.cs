using NotificationBarrage.Domain;

namespace NotificationBarrage.Services;

public sealed class BarrageQueue(TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly object _sync = new();
    private readonly Queue<BarrageMessage> _pending = new();
    private readonly Queue<DateTimeOffset> _accepted = new();
    private readonly Dictionary<Guid, DateTimeOffset> _ids = new();
    private readonly Dictionary<string, DateTimeOffset> _content = new();
    private bool _paused;
    private int _active;
    public int Count { get { lock (_sync) return _pending.Count; } }
    public int ActiveCount { get { lock (_sync) return _active; } }
    public bool IsPaused { get { lock (_sync) return _paused; } }

    public bool Enqueue(BarrageMessage message)
    {
        lock (_sync)
        {
            var now = _clock.GetUtcNow();
            foreach (var key in _ids.Where(p => now - p.Value >= TimeSpan.FromMinutes(5)).Select(p => p.Key).ToArray()) _ids.Remove(key);
            foreach (var key in _content.Where(p => now - p.Value >= TimeSpan.FromSeconds(5)).Select(p => p.Key).ToArray()) _content.Remove(key);
            if ((message.NotificationId != Guid.Empty && _ids.ContainsKey(message.NotificationId)) || _content.ContainsKey(message.DeduplicationKey)) return false;
            while (_accepted.TryPeek(out var time) && now - time >= TimeSpan.FromSeconds(1)) _accepted.Dequeue();
            if (_accepted.Count >= 3) return false;
            _accepted.Enqueue(now);
            if (message.NotificationId != Guid.Empty) _ids[message.NotificationId] = now;
            _content[message.DeduplicationKey] = now;
            _pending.Enqueue(message);
            Trim(_paused ? 10 : 100);
            return true;
        }
    }

    public bool TryDequeue(out BarrageMessage? message)
    {
        lock (_sync)
        {
            message = null;
            if (_paused || _active >= 5 || !_pending.TryDequeue(out message)) return false;
            _active++;
            return true;
        }
    }

    public void SetPaused(bool paused)
    {
        lock (_sync) { _paused = paused; if (paused) Trim(10); }
    }

    public void MarkActiveCompleted() { lock (_sync) _active = Math.Max(0, _active - 1); }
    private void Trim(int maximum) { while (_pending.Count > maximum) _pending.Dequeue(); }
}
