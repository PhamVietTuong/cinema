using System.Security.Claims;
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

    // The authenticated user behind this connection. A hold belongs to the (connection, user) pair — see
    // IBookingManager.LockSeat — so a connection id learned from elsewhere cannot be replayed by another account.
    private Guid? CurrentUserId()
    {
        var value = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.User?.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public async Task LockSeat(Guid showTimeId, Guid roomId, Guid seatId)
    {
        var userId = CurrentUserId();
        if (_bookingManager.IsSeatLocked(showTimeId, roomId, seatId, Context.ConnectionId, userId))
        {
            await Clients.Caller.SendAsync("SeatLockFailed", seatId, "Seat is already locked.");
            return;
        }

        if (!_bookingManager.LockSeat(showTimeId, roomId, seatId, Context.ConnectionId, userId))
        {
            await Clients.Caller.SendAsync("SeatLockFailed", seatId, "Too many seats are held on this connection.");
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(showTimeId, roomId));
        // Tell everyone ELSE the seat is taken. The locker's connection id is deliberately not sent: it must stay
        // private to its owner, and the locker already knows (it ignored its own broadcast anyway).
        await Clients.GroupExcept(RoomGroup(showTimeId, roomId), Context.ConnectionId).SendAsync("SeatLocked", seatId);
    }

    public async Task UnlockSeat(Guid showTimeId, Guid roomId, Guid seatId)
    {
        // Broadcast only a real release: a non-owner's unlock changes nothing, and announcing it would make
        // other viewers show a still-held seat as free.
        if (_bookingManager.UnlockSeat(showTimeId, roomId, seatId, Context.ConnectionId, CurrentUserId()))
        {
            await Clients.Group(RoomGroup(showTimeId, roomId)).SendAsync("SeatUnlocked", seatId);
        }
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

    public static string RoomGroup(Guid showTimeId, Guid roomId)
    {
        return $"room-{showTimeId}-{roomId}";
    }
}
