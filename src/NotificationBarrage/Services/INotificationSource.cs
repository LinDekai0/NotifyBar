using NotificationBarrage.Domain;

namespace NotificationBarrage.Services;

public interface INotificationSource
{
    event EventHandler<IncomingNotification>? NotificationReceived;
    event EventHandler<ListenerStatus>? StatusChanged;
    event EventHandler<NotificationSourcesChangedEventArgs>? SourcesChanged;
    IReadOnlyList<NotificationSourceInfo> Sources { get; }
    Task<NotificationAccessStatus> GetAccessStatusAsync();
    Task<NotificationAccessStatus> RequestAccessAsync();
    Task RefreshAsync(CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
}
