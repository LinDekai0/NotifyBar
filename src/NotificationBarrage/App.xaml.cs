using System.Windows;
using System.Windows.Threading;

namespace NotificationBarrage;

public partial class App : System.Windows.Application
{
    private readonly string[] _arguments;
    private readonly Services.AppLogger _logger = new();
    private AppHost? _host;
    private bool _isShuttingDown;
    public App() : this([]) { }
    public App(string[] arguments)
    {
        _arguments = arguments;
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _host = new AppHost(this);
            _host.Start(_arguments);
        }
        catch (Exception ex)
        {
            HandleStartupFailure(ex);
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _isShuttingDown = true;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        try { _host?.Dispose(); }
        catch (Exception ex) { _logger.Error("app-dispose-failed", ex); }
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        if (_isShuttingDown) return;
        if (IsRecoverable(e.Exception))
        {
            ReportRecoverableException("ui-unhandled", e.Exception);
            return;
        }
        ReportFatalException("ui-unhandled", e.Exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        if (_isShuttingDown) return;
        try
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_isShuttingDown) return;
                if (IsRecoverable(e.Exception)) ReportRecoverableException("task-unobserved", e.Exception);
                else ReportFatalException("task-unobserved", e.Exception);
            });
        }
        catch (Exception)
        {
            _logger.Error("task-unobserved", e.Exception);
        }
    }

    private static bool IsRecoverable(Exception exception) => exception is OperationCanceledException or IOException or UnauthorizedAccessException;

    private void ReportRecoverableException(string eventName, Exception exception)
    {
        if (_host is not null) _host.ReportUnhandledException(eventName, exception);
        else HandleStartupFailure(exception);
    }

    private void ReportFatalException(string eventName, Exception exception)
    {
        if (_host is not null) _host.ReportUnhandledException(eventName, exception);
        else _logger.Error(eventName, exception);
        try
        {
            MessageBox.Show(
                "通知弹幕遇到无法继续运行的错误，错误已记录到本机日志。请重新启动应用；若仍失败，请重新安装完整版 MSIX 后再试。",
                "通知弹幕",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception messageBoxException)
        {
            _logger.Error("app-fatal-error-prompt-failed", messageBoxException);
        }
        ShutdownSafely();
    }

    private void HandleStartupFailure(Exception exception)
    {
        _logger.Error("app-startup-failed", exception);
        try
        {
            MessageBox.Show(
                "通知弹幕启动失败，错误已记录到本机日志。请重新启动应用；若仍失败，请重新安装完整版 MSIX 后再试。",
                "通知弹幕",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception messageBoxException)
        {
            _logger.Error("app-startup-error-prompt-failed", messageBoxException);
        }
        ShutdownSafely();
    }

    private void ShutdownSafely()
    {
        if (_isShuttingDown) return;
        _isShuttingDown = true;
        try { Shutdown(); }
        catch (Exception ex) { _logger.Error("app-controlled-shutdown-failed", ex); }
    }
}
