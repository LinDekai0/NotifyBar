namespace NotificationBarrage.Domain;

public enum NotificationAccessStatus { Unknown, Allowed, Denied, NeedsPackage, Unavailable }
public sealed record ListenerStatus(NotificationAccessStatus Access, string Message);
