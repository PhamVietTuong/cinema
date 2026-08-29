using Cinema.Business.Contracts;
using Cinema.Foundation.Logging;

namespace Cinema.Service.WebApiHost.Services;

/// <summary>
/// Periodically cancels Pending invoices that were never paid, freeing the seats they held.
/// Complements the in-memory SignalR seat-lock expiry (which only covers live connections).
/// </summary>
public class PendingBookingReaper : BackgroundService
{
    private static readonly TimeSpan _interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan _holdWindow = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;

    public PendingBookingReaper(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var bookings = scope.ServiceProvider.GetRequiredService<IBookingManager>();
                var expired = await bookings.ExpireStalePendingBookingsAsync(_holdWindow);
                if (expired > 0)
                {
                    LogProvider.Current.Information($"PendingBookingReaper: expired {expired} stale pending invoice(s).");
                }
            }
            catch (Exception e)
            {
                LogProvider.Current.Fatal(e, $"PendingBookingReaper->Exception: {e.GetType()}, {e.Message}");
            }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
