using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using NotificationBarrage.Domain;
using NotificationBarrage.Services;
using NotificationBarrage.UI;

namespace NotificationBarrage;

public sealed class AppHost : IDisposable
{
    private readonly App _app;
    private readonly SettingsStore _store = new();
    private readonly AppLogger _logger = new();
    private readonly MessageFilter _filter = new();
    private readonly BarrageQueue _queue = new();
    private readonly WindowsNotificationSource _source;
    private readonly OverlayWindow _overlay = new();
    private readonly Forms.NotifyIcon _tray;
    private readonly Forms.ToolStripMenuItem _pause;
    private readonly DispatcherTimer _drain;
    private readonly CancellationTokenSource _stop = new();
    private SettingsWindow? _window;
    private AppSettings _settings;
    private ListenerStatus _status = new(NotificationAccessStatus.Unknown, "等待连接 Windows 通知。");
    private bool _exiting;

    public AppHost(App app)
    {
        _app = app;
        _settings = _store.Load();
        _source = new WindowsNotificationSource(_logger);
        _tray = new Forms.NotifyIcon { Text = "NotifyBar", Icon = System.Drawing.SystemIcons.Information };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开设置", null, (_, _) => OpenSettings());
        _pause = new Forms.ToolStripMenuItem("暂停弹幕", null, (_, _) => TogglePause()); menu.Items.Add(_pause);
        menu.Items.Add("测试弹幕", null, (_, _) => TestMessage());
        menu.Items.Add("重新检查权限", null, async (_, _) => await CheckPermissionAsync());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, async (_, _) => await ExitAsync());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => OpenSettings();
        _overlay.MessageCompleted += (_, _) => _queue.MarkActiveCompleted();
        _source.NotificationReceived += (_, message) => _app.Dispatcher.BeginInvoke(() =>
        {
            if (!_exiting && _filter.TryCreate(message, _settings, out var item)) _queue.Enqueue(item!);
        });
        _source.StatusChanged += (_, status) => _app.Dispatcher.BeginInvoke(() =>
        {
            if (_exiting) return;
            _status = status; _window?.SetStatus(status);
            _tray.Text = status.Access == NotificationAccessStatus.Allowed ? "NotifyBar · 正在监听" : "NotifyBar · 尚未连接通知";
        });
        _drain = new DispatcherTimer(TimeSpan.FromMilliseconds(120), DispatcherPriority.Background, (_, _) => Drain(), app.Dispatcher);
    }

    public void Start(string[] args)
    {
        _tray.Visible = true;
        _overlay.Show(); _overlay.ApplySettings(_settings);
        var demo = args.Contains("--demo") || args.Contains("--smoke");
        if (demo)
        {
            _status = new(NotificationAccessStatus.NeedsPackage, "显示测试 · 未连接系统通知。点击测试弹幕检查游戏覆盖效果。");
            OpenSettings(); TestMessage();
            if (args.Contains("--capture"))
            {
                _app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    _window?.CapturePreview(Path.Combine(AppContext.BaseDirectory, "settings-preview.png"));
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "overlay-diagnostics.json"), System.Text.Json.JsonSerializer.Serialize(_overlay.Diagnostics()));
                }));
            }
            if (args.Contains("--smoke"))
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
                timer.Tick += async (_, _) =>
                {
                    timer.Stop();
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-complete.json"), System.Text.Json.JsonSerializer.Serialize(new { QueueActive = _queue.ActiveCount, OverlayActive = _overlay.ActiveItems, Pending = _queue.Count }));
                    await ExitAsync();
                }; timer.Start();
            }
        }
        else _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var access = await _source.GetAccessStatusAsync();
            var startupEnabled = await StartupRegistration.GetEnabledAsync();
            if (startupEnabled.HasValue) _settings = _settings with { StartWithWindows = startupEnabled.Value };
            await _source.StartAsync(_stop.Token);
            if (access != NotificationAccessStatus.Allowed || _store.LoadWarning is not null) OpenSettings();
            if (_store.LoadWarning is not null) _window?.ShowResult(_store.LoadWarning);
        }
        catch (Exception ex) { _logger.Error("startup-failed", ex); OpenSettings(); _window?.ShowResult("通知服务启动失败，仍可测试弹幕。请检查权限或重新安装 MSIX。"); }
    }

    private void OpenSettings()
    {
        if (_exiting) return;
        if (_window is null)
        {
            _window = new SettingsWindow(_settings, SaveAsync, TestMessage, ConnectAsync,
                () => ShowError(NotificationPermissionService.OpenNotificationSettings()),
                () => ShowError(NotificationPermissionService.OpenBannerSettings()));
            _window.Closed += (_, _) => _window = null;
        }
        _window.SetStatus(_status); _window.Show(); _window.Activate();
    }

    private async Task<string?> SaveAsync(AppSettings settings)
    {
        var changedStartup = settings.StartWithWindows != _settings.StartWithWindows;
        if (changedStartup)
        {
            var result = await StartupRegistration.SetEnabledAsync(settings.StartWithWindows);
            if (result.Error is not null) return result.Error;
        }
        try
        {
            _store.Save(settings); _settings = settings; _overlay.ApplySettings(settings); return null;
        }
        catch (Exception ex)
        {
            _logger.Error("settings-save-failed", ex);
            if (changedStartup) await StartupRegistration.SetEnabledAsync(_settings.StartWithWindows);
            return "设置未保存：无法写入本机配置目录，请检查磁盘或目录权限。";
        }
    }

    private void TogglePause()
    {
        _queue.SetPaused(!_queue.IsPaused); _pause.Text = _queue.IsPaused ? "恢复弹幕" : "暂停弹幕";
        if (_queue.IsPaused) _overlay.ClearMessages();
    }

    private void TestMessage()
    {
        if (_queue.IsPaused) { _window?.ShowResult("已暂停，请从托盘恢复弹幕后再测试。"); _tray.ShowBalloonTip(2500, "NotifyBar 已暂停", "右键托盘图标选择「恢复弹幕」后再测试。", Forms.ToolTipIcon.Info); return; }
        var message = new BarrageMessage("微信", "小伙伴", "等你这局结束，一起开黑！", DateTimeOffset.Now, Guid.NewGuid().ToString()) { NotificationId = Guid.NewGuid(), IsTest = true };
        if (!_queue.Enqueue(message)) { _window?.ShowResult("测试消息发送较快，请稍等一秒再试。"); return; }
        _window?.ShowResult("测试弹幕已发送。关闭设置窗口后也能从托盘继续测试。");
    }

    private void Drain()
    {
        if (_exiting) return;
        while (_overlay.AvailableSlots > 0 && _queue.TryDequeue(out var item))
        {
            // Re-check source toggles at display time, so disabling a source also filters its backlog.
            if (!item!.IsTest && (item.Source == "QQ" ? !_settings.EnableQQ : !_settings.EnableWeChat)) { _queue.MarkActiveCompleted(); continue; }
            try { if (!_overlay.ShowMessage(item!, _settings)) { _queue.MarkActiveCompleted(); break; } }
            catch (Exception ex) { _queue.MarkActiveCompleted(); _logger.Error("overlay-failed", ex); }
        }
    }

    private async Task ConnectAsync() { await _source.RequestAccessAsync(); await _source.StartAsync(_stop.Token); }
    private async Task CheckPermissionAsync() { await _source.GetAccessStatusAsync(); OpenSettings(); }
    private void ShowError(string? error) { if (error is not null) _window?.ShowResult(error); }

    public void ReportUnhandledException(string eventName, Exception exception)
    {
        _logger.Error(eventName, exception);
        const string message = "应用遇到错误，已记录到本机日志。请打开设置检查通知权限；若持续发生，请重新启动或重新安装完整版 MSIX。";
        try { _tray.ShowBalloonTip(5000, "NotifyBar 需要处理", message, Forms.ToolTipIcon.Warning); }
        catch (Exception trayException) { _logger.Error("tray-error-notification-failed", trayException); }
        try { _window?.ShowResult(message); }
        catch (Exception windowException) { _logger.Error("settings-error-notification-failed", windowException); }
    }

    private async Task ExitAsync()
    {
        if (_exiting) return; _exiting = true; _drain.Stop(); _stop.Cancel();
        await _source.StopAsync(); _app.Shutdown();
    }
    public void Dispose() { _exiting = true; _drain.Stop(); _stop.Cancel(); _tray.Visible = false; _tray.Dispose(); _overlay.Close(); _stop.Dispose(); }
}
