using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NotificationBarrage.Domain;

namespace NotificationBarrage.Services;

public sealed class MessageFilter
{
    private static readonly string[] QQNames = ["QQ", "腾讯QQ", "腾讯 QQ", "QQ NT", "QQNT", "Tencent.QQ"];
    private static readonly string[] WeChatNames = ["微信", "WeChat", "Weixin", "腾讯微信", "Tencent.WeChat", "Tencent.Weixin"];
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    public bool TryCreate(IncomingNotification notification, AppSettings settings, out BarrageMessage? message)
    {
        message = null;
        var source = Matches(notification, QQNames) ? "QQ" : Matches(notification, WeChatNames) ? "微信" : null;
        if (source is null || (source == "QQ" ? !settings.EnableQQ : !settings.EnableWeChat)) return false;
        var title = Normalize(notification.Title);
        var body = Normalize(notification.Body);
        if (title.Length == 0 && body.Length == 0) return false;
        // Hash the complete text, so different long messages are not merged after truncation.
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{source}\0{title}\0{body}")));
        message = new(source, Truncate(title, 60), Truncate(body, settings.Normalize().MaxBodyLength), notification.ReceivedAt, key)
        { NotificationId = notification.Id };
        return true;
    }

    private static bool Matches(IncomingNotification item, string[] names)
    {
        var name = (item.AppName ?? "").Trim();
        var id = (item.AppId ?? "").Trim();
        if (names.Any(n => string.Equals(name, n, StringComparison.OrdinalIgnoreCase) || string.Equals(id, n, StringComparison.OrdinalIgnoreCase))) return true;
        // Packaged apps use PackageFamilyName!Application. Match known publisher tokens only.
        return names.Where(n => n.StartsWith("Tencent.", StringComparison.Ordinal)).Any(n =>
            id.StartsWith(n + "_", StringComparison.OrdinalIgnoreCase) || id.StartsWith(n + "!", StringComparison.OrdinalIgnoreCase));
    }

    public static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? "" : Spaces.Replace(value, " ").Trim();

    public static string Truncate(string value, int maximum)
    {
        var starts = StringInfo.ParseCombiningCharacters(value);
        return starts.Length <= maximum ? value : value[..starts[maximum]] + "…";
    }
}
