namespace NotificationBarrage.Domain;

public sealed record BarrageMessage(
    string Source,
    string Title,
    string Body,
    DateTimeOffset ReceivedAt,
    string DeduplicationKey)
{
    public Guid NotificationId { get; init; }
    public bool IsTest { get; init; }
}
