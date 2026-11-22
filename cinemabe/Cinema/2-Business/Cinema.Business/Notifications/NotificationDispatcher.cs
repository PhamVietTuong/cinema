using System.Threading.Channels;
using Cinema.Business.Contracts;
using Cinema.Foundation.Logging;

namespace Cinema.Business.Notifications;

/// <summary>
/// Process-local queue behind <see cref="INotificationDispatcher"/>. A single reader (the host's background
/// service) drains it via <see cref="RunAsync"/>. The queue is bounded: when full, the oldest item is dropped
/// and logged rather than blocking the request path.
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly Channel<Func<Task>> _channel = Channel.CreateBounded<Func<Task>>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public void Enqueue(Func<Task> work)
    {
        _channel.Writer.TryWrite(work);
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var work in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await work();
                }
                catch (Exception e)
                {
                    LogProvider.Current.Warning(e, $"{nameof(NotificationDispatcher)} work item failed: {e.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down.
        }
    }
}
