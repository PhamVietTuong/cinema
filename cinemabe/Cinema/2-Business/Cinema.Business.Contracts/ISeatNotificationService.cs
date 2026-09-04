namespace Cinema.Business.Contracts;

/// <summary>
/// Pushes real-time seat-map updates to viewers of a showtime/room. The dev default is a no-op
/// (logs only); the Web API host wires in a SignalR-backed implementation that broadcasts to the
/// same <c>BookingHub</c> group used for seat locking. Implementations must never throw — a
/// notification failure can't be allowed to fail an already-committed booking.
/// </summary>
public interface ISeatNotificationService
{
    Task NotifySeatsBookedAsync(Guid showTimeId, Guid roomId, IReadOnlyList<Guid> seatIds);
}
