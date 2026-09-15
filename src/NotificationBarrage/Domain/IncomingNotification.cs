namespace NotificationBarrage.Domain;

public sealed record IncomingNotification(
    Guid Id,
    string AppId,
    string AppName,
    string Title,
    string Body,
    DateTimeOffset ReceivedAt);
