using Cinema.Business.Notifications;

namespace Cinema.Service.WebApiHost.Services;

/// <summary>Drains <see cref="NotificationDispatcher"/> so confirmation emails/SMS are sent off the request path.</summary>
public class NotificationDispatchService : BackgroundService
{
    private readonly NotificationDispatcher _dispatcher;

    public NotificationDispatchService(NotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _dispatcher.RunAsync(stoppingToken);
    }
}
