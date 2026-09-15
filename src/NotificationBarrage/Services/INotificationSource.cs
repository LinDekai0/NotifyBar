using NotificationBarrage.Domain;

namespace NotificationBarrage.Services;

public interface INotificationSource
{
    event EventHandler<IncomingNotification>? NotificationReceived;
    event EventHandler<ListenerStatus>? StatusChanged;
    Task<NotificationAccessStatus> GetAccessStatusAsync();
    Task<NotificationAccessStatus> RequestAccessAsync();
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
}
