using Cinema.Business.Contracts;
using Cinema.Foundation.Logging;
using Cinema.Service.WebApiHost.Hubs;
using Microsoft.AspNetCore.SignalR;

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
    private readonly IHubContext<BookingHub> _hub;

    public PendingBookingReaper(IServiceScopeFactory scopeFactory, IHubContext<BookingHub> hub)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var bookings = scope.ServiceProvider.GetRequiredService<IBookingManager>();
                // Seat locks expire lazily when read; sweep the dead ones and tell the room so clients stop showing them held.
                foreach (var (showTimeId, roomId, seatId) in bookings.SweepExpiredLocks())
                {
                    await _hub.Clients.Group(BookingHub.RoomGroup(showTimeId, roomId)).SendAsync("SeatUnlocked", seatId, stoppingToken);
                }
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
