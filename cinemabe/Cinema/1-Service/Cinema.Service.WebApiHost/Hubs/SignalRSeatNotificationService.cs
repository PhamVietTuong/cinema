using Cinema.Business.Contracts;
using Cinema.Foundation.Logging;
using Microsoft.AspNetCore.SignalR;

namespace Cinema.Service.WebApiHost.Hubs;

/// <summary>
/// Broadcasts newly-booked seats to <see cref="BookingHub"/>'s room group in real time, so other
/// viewers of the same showtime/room see them go unavailable without waiting for a manual refresh.
/// Never throws — a broadcast failure must not turn an already-committed booking into an error.
/// </summary>
public class SignalRSeatNotificationService : ISeatNotificationService
{
    private readonly IHubContext<BookingHub> _hub;

    public SignalRSeatNotificationService(IHubContext<BookingHub> hub)
    {
        _hub = hub;
    }

    public async Task NotifySeatsBookedAsync(Guid showTimeId, Guid roomId, IReadOnlyList<Guid> seatIds)
    {
        if (seatIds.Count == 0)
        {
            return;
        }

        try
        {
            await _hub.Clients.Group(BookingHub.RoomGroup(showTimeId, roomId)).SendAsync("SeatBooked", seatIds);
        }
        catch (Exception e)
        {
            LogProvider.Current.Fatal(e, $"SignalRSeatNotificationService->Exception broadcasting SeatBooked for ShowTimeId={showTimeId} RoomId={roomId}: {e.Message}");
        }
    }
}
