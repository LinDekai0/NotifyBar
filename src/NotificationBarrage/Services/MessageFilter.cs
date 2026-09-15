using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NotificationBarrage.Domain;

namespace NotificationBarrage.Services;

public sealed class MessageFilter
{
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    public bool TryCreate(IncomingNotification notification, AppSettings settings, out BarrageMessage? message)
    {
        message = null;
        var appId = notification.AppId?.Trim() ?? "";
        if (!settings.IsSourceEnabled(appId)) return false;
        var source = Normalize(notification.AppName);
        if (source.Length == 0) source = appId;
        var title = Normalize(notification.Title);
        var body = Normalize(notification.Body);
        if (title.Length == 0 && body.Length == 0) return false;
        // Hash the complete text, so different long messages are not merged after truncation.
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{appId}\0{title}\0{body}")));
        message = new(appId, source, Truncate(title, 60), Truncate(body, settings.Normalize().MaxBodyLength), notification.ReceivedAt, key)
        { NotificationId = notification.Id };
        return true;
    }

    public static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? "" : Spaces.Replace(value, " ").Trim();

    public static string Truncate(string value, int maximum)
    {
        var starts = StringInfo.ParseCombiningCharacters(value);
        return starts.Length <= maximum ? value : value[..starts[maximum]] + "…";
    }
}
