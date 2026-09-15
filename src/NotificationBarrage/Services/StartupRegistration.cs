#if !PREVIEW
using Windows.ApplicationModel;
#endif

namespace NotificationBarrage.Services;

public static class StartupRegistration
{
    public const string TaskId = "NotificationBarrageStartup";
    public static async Task<bool?> GetEnabledAsync()
    {
#if !PREVIEW
        try { var task = await StartupTask.GetAsync(TaskId); return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy; }
        catch { return null; }
#else
        await Task.CompletedTask; return null;
#endif
    }
    public static async Task<(bool Enabled, string? Error)> SetEnabledAsync(bool enabled)
    {
#if !PREVIEW
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            if (!enabled)
            {
                task.Disable();
                var disabledState = (await StartupTask.GetAsync(TaskId)).State;
                return disabledState is StartupTaskState.Disabled or StartupTaskState.DisabledByUser
                    ? (false, null)
                    : (true, "Windows 未能关闭开机启动，请在任务管理器的「启动应用」中禁用 NotifyBar。");
            }
            var enabledState = await task.RequestEnableAsync();
            return enabledState is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy
                ? (true, null)
                : (false, "Windows 未允许开机启动，请在任务管理器的「启动应用」中启用 NotifyBar。");
        }
        catch
        {
            return (false, enabled
                ? "开机启动需要先安装 MSIX，并允许此应用启动。"
                : "无法关闭开机启动，请在任务管理器的「启动应用」中禁用 NotifyBar。");
        }
#else
        await Task.CompletedTask;
        return (false, enabled ? "预览版不支持开机启动，请安装完整版 MSIX。" : null);
#endif
    }
}
