using Cinema.Business.Contracts;
using Cinema.Foundation.Logging;

namespace Cinema.Business.Notifications;

/// <summary>
/// Development default — logs instead of broadcasting. Lets booking flows (and the test/seeder exe)
/// run without SignalR. Replaced by the Web API host's SignalR-backed sender at startup.
/// </summary>
public class NoOpSeatNotificationService : ISeatNotificationService
{
    public Task NotifySeatsBookedAsync(Guid showTimeId, Guid roomId, IReadOnlyList<Guid> seatIds)
    {
        LogProvider.Current.Information($"[SeatNotification:skipped] ShowTimeId={showTimeId} RoomId={roomId} SeatIds={string.Join(",", seatIds)}");
        return Task.CompletedTask;
    }
}
