namespace NotificationBarrage.Domain;

public sealed record BarrageMessage(
    string AppId,
    string Source,
    string Title,
    string Body,
    DateTimeOffset ReceivedAt,
    string DeduplicationKey)
{
    public Guid NotificationId { get; init; }
    public bool IsTest { get; init; }
}

public static class BarrageDisplayGate
{
    public static bool ShouldDisplay(BarrageMessage message, AppSettings settings) =>
        message.IsTest || settings.IsSourceEnabled(message.AppId);
}
