using Cinema.Business.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace Cinema.Service.WebApiHost.Hubs;

public class BookingHub : Hub
{
    private readonly IBookingManager _bookingManager;

    public BookingHub(IBookingManager bookingManager) => _bookingManager = bookingManager;

    public async Task LockSeat(Guid showTimeId, Guid roomId, Guid seatId)
    {
        if (_bookingManager.IsSeatLocked(showTimeId, roomId, seatId, Context.ConnectionId))
        {
            await Clients.Caller.SendAsync("SeatLockFailed", seatId, "Seat is already locked.");
            return;
        }

        _bookingManager.LockSeat(showTimeId, roomId, seatId, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room-{showTimeId}-{roomId}");
        await Clients.Group($"room-{showTimeId}-{roomId}").SendAsync("SeatLocked", seatId, Context.ConnectionId);
    }

    public async Task UnlockSeat(Guid showTimeId, Guid roomId, Guid seatId)
    {
        _bookingManager.UnlockSeat(showTimeId, roomId, seatId, Context.ConnectionId);
        await Clients.Group($"room-{showTimeId}-{roomId}").SendAsync("SeatUnlocked", seatId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Auto-unlock seats when client disconnects (simplified - in production track per connection)
        await base.OnDisconnectedAsync(exception);
    }
}
