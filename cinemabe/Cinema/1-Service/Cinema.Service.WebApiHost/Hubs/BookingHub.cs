using Cinema.Business.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Cinema.Service.WebApiHost.Hubs;

[Authorize]
public class BookingHub : Hub
{
    private readonly IBookingManager _bookingManager;

    public BookingHub(IBookingManager bookingManager)
    {
        _bookingManager = bookingManager;
    }

    public Task JoinRoom(Guid showTimeId, Guid roomId)
        => Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(showTimeId, roomId));

    public async Task LockSeat(Guid showTimeId, Guid roomId, Guid seatId)
    {
        if (_bookingManager.IsSeatLocked(showTimeId, roomId, seatId, Context.ConnectionId))
        {
            await Clients.Caller.SendAsync("SeatLockFailed", seatId, "Seat is already locked.");
            return;
        }

        _bookingManager.LockSeat(showTimeId, roomId, seatId, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(showTimeId, roomId));
        await Clients.Group(RoomGroup(showTimeId, roomId)).SendAsync("SeatLocked", seatId, Context.ConnectionId);
    }

    public async Task UnlockSeat(Guid showTimeId, Guid roomId, Guid seatId)
    {
        _bookingManager.UnlockSeat(showTimeId, roomId, seatId, Context.ConnectionId);
        await Clients.Group(RoomGroup(showTimeId, roomId)).SendAsync("SeatUnlocked", seatId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Release every seat this connection still holds and notify the rooms.
        // (SignalR removes the connection from its groups automatically.)
        foreach (var (showTimeId, roomId, seatId) in _bookingManager.ReleaseConnectionLocks(Context.ConnectionId))
        {
            await Clients.Group(RoomGroup(showTimeId, roomId)).SendAsync("SeatUnlocked", seatId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private static string RoomGroup(Guid showTimeId, Guid roomId)
    {
        return $"room-{showTimeId}-{roomId}";
    }
}
