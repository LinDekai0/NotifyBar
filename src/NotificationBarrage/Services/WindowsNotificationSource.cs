using NotificationBarrage.Domain;
#if !PREVIEW
using Windows.ApplicationModel;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
#endif

namespace NotificationBarrage.Services;

public sealed class WindowsNotificationSource(AppLogger logger) : INotificationSource
{
#if PREVIEW
    public event EventHandler<IncomingNotification>? NotificationReceived { add { } remove { } }
#else
    public event EventHandler<IncomingNotification>? NotificationReceived;
#endif
    public event EventHandler<ListenerStatus>? StatusChanged;
    private ListenerStatus? _status;
#if !PREVIEW
    private CancellationTokenSource? _stop;
    private Task? _loop;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private UserNotificationListener? _listener;
    private bool _subscribed;
    private bool _eventAttempted;
    private readonly NotificationSnapshotTracker _tracker = new();

    public Task<NotificationAccessStatus> GetAccessStatusAsync()
    {
        try
        {
            if (!HasIdentity()) return Task.FromResult(Set(NotificationAccessStatus.NeedsPackage, "请安装 MSIX 后连接 Windows 通知；当前可测试弹幕。"));
            _listener ??= UserNotificationListener.Current;
            return Task.FromResult(MapAccess(_listener.GetAccessStatus()));
        }
        catch (Exception ex)
        {
            logger.Error("notification-access-failed", ex);
            return Task.FromResult(Set(NotificationAccessStatus.Unavailable, "通知服务暂不可用，将自动重试。"));
        }
    }

    public async Task<NotificationAccessStatus> RequestAccessAsync()
    {
        if (await GetAccessStatusAsync() == NotificationAccessStatus.NeedsPackage) return NotificationAccessStatus.NeedsPackage;
        try
        {
            // Called from the visible settings window's button, on its UI thread.
            _listener ??= UserNotificationListener.Current;
            return MapAccess(await _listener.RequestAccessAsync());
        }
        catch (Exception ex)
        {
            logger.Error("notification-request-failed", ex);
            return Set(NotificationAccessStatus.Unavailable, "申请权限失败，请打开系统通知访问设置。必要时重新安装 MSIX。 ");
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loop is not null) return Task.CompletedTask;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunAsync(_stop.Token);
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken token)
    {
        var attempt = 0;
        int[] retrySeconds = [1, 5, 15, 60];
        try
        {
            while (!token.IsCancellationRequested)
            {
                var delay = 1000;
                try
                {
                    if (await GetAccessStatusAsync() != NotificationAccessStatus.Allowed)
                    {
                        _tracker.Reset();
                        delay = 5000;
                    }
                    else
                    {
                        if (!_eventAttempted)
                        {
                            _eventAttempted = true;
                            try { _listener!.NotificationChanged += OnChanged; _subscribed = true; }
                            catch (Exception ex) { logger.Error("notification-event-unavailable-polling", ex); }
                        }
                        var notifications = await _listener!.GetNotificationsAsync(NotificationKinds.Toast).AsTask(token);
                        token.ThrowIfCancellationRequested();
                        var current = new List<IncomingNotification>();
                        foreach (var notification in notifications.OrderBy(n => n.CreationTime))
                        {
                            try
                            {
                                var binding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
                                var message = NotificationMapper.Map(notification.Id, notification.AppInfo.AppUserModelId,
                                    notification.AppInfo.DisplayInfo.DisplayName,
                                    binding?.GetTextElements().Select(t => t.Text) ?? Array.Empty<string>(), notification.CreationTime);
                                current.Add(message);
                            }
                            catch (Exception ex) { logger.Error("notification-map-failed", ex); }
                        }
                        foreach (var message in _tracker.Update(current))
                            if (!token.IsCancellationRequested) NotificationReceived?.Invoke(this, message);
                        attempt = 0;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    logger.Error("notification-listener-failed", ex);
                    Set(NotificationAccessStatus.Unavailable, "通知监听暂时中断，正在重连。测试弹幕仍可使用。");
                    _tracker.Reset();
                    delay = retrySeconds[Math.Min(attempt++, retrySeconds.Length - 1)] * 1000;
                }
                await _wake.WaitAsync(delay, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private void OnChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
    {
        if (args.ChangeKind != UserNotificationChangedKind.Added) return;
        try { _wake.Release(); } catch (SemaphoreFullException) { }
    }

    public async Task StopAsync()
    {
        if (_stop is null) return;
        _stop.Cancel();
        if (_subscribed)
        {
            try { _listener!.NotificationChanged -= OnChanged; } catch (Exception ex) { logger.Error("notification-unsubscribe", ex); }
            _subscribed = false;
        }
        if (_loop is not null) await _loop;
        _loop = null;
        _tracker.Reset();
        _eventAttempted = false;
        _stop.Dispose();
        _stop = null;
    }

    private static bool HasIdentity() { try { return !string.IsNullOrEmpty(Package.Current.Id.Name); } catch { return false; } }
    private NotificationAccessStatus MapAccess(UserNotificationListenerAccessStatus status) => status switch
    {
        UserNotificationListenerAccessStatus.Allowed => Set(NotificationAccessStatus.Allowed, "已连接 Windows 通知 · 等待新的 QQ / 微信消息"),
        UserNotificationListenerAccessStatus.Denied => Set(NotificationAccessStatus.Denied, "未获通知访问权限，请在系统设置中允许此应用访问通知。"),
        _ => Set(NotificationAccessStatus.Unknown, "点击「连接通知」，在 Windows 提示中允许通知访问。")
    };
#else
    public Task<NotificationAccessStatus> GetAccessStatusAsync()
    {
        _ = logger;
        return Task.FromResult(Set(NotificationAccessStatus.NeedsPackage, "预览模式 · 未连接系统通知。可测试弹幕、调整样式；接收消息请安装完整版 MSIX。"));
    }
    public Task<NotificationAccessStatus> RequestAccessAsync() => GetAccessStatusAsync();
    public async Task StartAsync(CancellationToken cancellationToken) { await GetAccessStatusAsync(); }
    public Task StopAsync() => Task.CompletedTask;
#endif

    private NotificationAccessStatus Set(NotificationAccessStatus access, string message)
    {
        var next = new ListenerStatus(access, message);
        if (_status != next) { _status = next; StatusChanged?.Invoke(this, next); }
        return access;
    }
}
