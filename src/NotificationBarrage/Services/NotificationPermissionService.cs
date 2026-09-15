using System.Diagnostics;

namespace NotificationBarrage.Services;

public static class NotificationPermissionService
{
    public static string? OpenNotificationSettings() => Open("ms-settings:privacy-notifications");
    public static string? OpenBannerSettings() => Open("ms-settings:notifications");
    private static string? Open(string uri)
    {
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); return null; }
        catch { return "无法打开系统设置。请手动进入 Windows 设置 → 隐私和安全性 → 通知。"; }
    }
}
