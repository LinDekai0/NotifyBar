using System.Security.Cryptography;
using System.Text;

namespace NotificationBarrage.Domain;

public static class NotificationMapper
{
    public static IncomingNotification Map(uint id, string appId, string appName, IEnumerable<string> text, DateTimeOffset created)
    {
        var lines = text.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{appId}\0{id}\0{created.UtcTicks}"));
        return new(new Guid(bytes.AsSpan(0, 16)), appId, appName, lines.FirstOrDefault() ?? "", string.Join("\n", lines.Skip(1)), created);
    }
}
