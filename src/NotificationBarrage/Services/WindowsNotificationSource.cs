using NotificationBarrage.Domain;
#if !PREVIEW
using Windows.ApplicationModel;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
#endif

namespace NotificationBarrage.Services;

public sealed class WindowsNotificationSource : INotificationSource
{
    private readonly AppLogger _logger;
    private readonly NotificationSnapshotProcessor _processor = new();

    public WindowsNotificationSource(AppLogger logger)
    {
        _logger = logger;
        _processor.SourcesChanged += (_, args) => SourcesChanged?.Invoke(this, args);
    }

#if PREVIEW
    public event EventHandler<IncomingNotification>? NotificationReceived { add { } remove { } }
#else
    public event EventHandler<IncomingNotification>? NotificationReceived;
#endif
    public event EventHandler<ListenerStatus>? StatusChanged;
    public event EventHandler<NotificationSourcesChangedEventArgs>? SourcesChanged;
    public IReadOnlyList<NotificationSourceInfo> Sources => _processor.Sources;
    private ListenerStatus? _status;
#if !PREVIEW
    private CancellationTokenSource? _stop;
    private Task? _loop;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private UserNotificationListener? _listener;
    private bool _subscribed;
    private bool _eventAttempted;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

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
            _logger.Error("notification-access-failed", ex);
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
            _logger.Error("notification-request-failed", ex);
            return Set(NotificationAccessStatus.Unavailable, "申请权限失败，请打开系统通知访问设置。必要时重新安装 MSIX。 ");
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loop is not null) return Task.CompletedTask;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Run the WinRT polling loop on the thread pool even when the first
        // awaits complete synchronously. This prevents snapshot mapping from
        // ever starting on the WPF Dispatcher thread.
        _loop = Task.Run(() => RunAsync(_stop.Token), _stop.Token);
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
                    if (await GetAccessStatusAsync().ConfigureAwait(false) != NotificationAccessStatus.Allowed)
                    {
                        _processor.Reset();
                        delay = 5000;
                    }
                    else
                    {
                        if (!_eventAttempted)
                        {
                            _eventAttempted = true;
                            try { _listener!.NotificationChanged += OnChanged; _subscribed = true; }
                            catch (Exception ex) { _logger.Error("notification-event-unavailable-polling", ex); }
                        }
                        await RefreshCoreAsync(token).ConfigureAwait(false);
                        attempt = 0;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.Error("notification-listener-failed", ex);
                    Set(NotificationAccessStatus.Unavailable, "通知监听暂时中断，正在重连。测试弹幕仍可使用。");
                    _processor.Reset();
                    delay = retrySeconds[Math.Min(attempt++, retrySeconds.Length - 1)] * 1000;
                }
                await _wake.WaitAsync(delay, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (await GetAccessStatusAsync().ConfigureAwait(false) != NotificationAccessStatus.Allowed) return;
        await RefreshCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RefreshCoreAsync(CancellationToken token)
    {
        await _refreshGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var accessRevision = _processor.AccessRevision;
            var notifications = await _listener!.GetNotificationsAsync(NotificationKinds.Toast).AsTask(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            // An access check may run while the WinRT snapshot is in flight.
            if (accessRevision != _processor.AccessRevision) return;
            var current = new List<IncomingNotification>();
            foreach (var notification in notifications.OrderBy(item => item.CreationTime))
            {
                try
                {
                    var appId = notification.AppInfo.AppUserModelId;
                    if (IsOwnApplication(appId)) continue;
                    var binding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
                    current.Add(NotificationMapper.Map(notification.Id, appId,
                        notification.AppInfo.DisplayInfo.DisplayName,
                        binding?.GetTextElements().Select(text => text.Text) ?? Array.Empty<string>(), notification.CreationTime));
                }
                catch (Exception ex) { _logger.Error("notification-map-failed", ex); }
            }

            foreach (var message in _processor.Update(current))
                if (!token.IsCancellationRequested) NotificationReceived?.Invoke(this, message);
        }
        finally { _refreshGate.Release(); }
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
            try { _listener!.NotificationChanged -= OnChanged; } catch (Exception ex) { _logger.Error("notification-unsubscribe", ex); }
            _subscribed = false;
        }
        if (_loop is not null) await _loop.ConfigureAwait(false);
        _loop = null;
        _processor.Reset();
        _eventAttempted = false;
        _stop.Dispose();
        _stop = null;
    }

    private static bool HasIdentity() { try { return !string.IsNullOrEmpty(Package.Current.Id.Name); } catch { return false; } }
    private static bool IsOwnApplication(string appId)
    {
        try { return appId.StartsWith(Package.Current.Id.FamilyName + "!", StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }
    private NotificationAccessStatus MapAccess(UserNotificationListenerAccessStatus status) => status switch
    {
        UserNotificationListenerAccessStatus.Allowed => Set(NotificationAccessStatus.Allowed, "已连接 Windows 通知 · 等待所选应用的新通知"),
        UserNotificationListenerAccessStatus.Denied => Set(NotificationAccessStatus.Denied, "未获通知访问权限，请在系统设置中允许此应用访问通知。"),
        _ => Set(NotificationAccessStatus.Unknown, "点击「连接通知」，在 Windows 提示中允许通知访问。")
    };
#else
    public Task<NotificationAccessStatus> GetAccessStatusAsync()
    {
        _ = _logger;
        return Task.FromResult(Set(NotificationAccessStatus.NeedsPackage, "预览模式 · 未连接系统通知。可测试弹幕、调整样式；接收消息请安装完整版 MSIX。"));
    }
    public Task<NotificationAccessStatus> RequestAccessAsync() => GetAccessStatusAsync();
    public Task RefreshAsync(CancellationToken cancellationToken = default) => GetAccessStatusAsync();
    public async Task StartAsync(CancellationToken cancellationToken) { await GetAccessStatusAsync(); }
    public Task StopAsync() => Task.CompletedTask;
#endif

    private NotificationAccessStatus Set(NotificationAccessStatus access, string message)
    {
        _processor.ObserveAccess(access);
        var next = new ListenerStatus(access, message);
        if (_status != next) { _status = next; StatusChanged?.Invoke(this, next); }
        return access;
    }
}
