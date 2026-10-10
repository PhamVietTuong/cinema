using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface ISeatStore : IGenericStore<Seat>
{
    Task<IEnumerable<Seat>> GetByRoomAsync(Guid roomId);

    /// <summary>Every seat of a room (active or not), untracked, ordered for the seat-map editor. Must be
    /// untracked: after ShiftRowLeftAsync's set-based ExecuteUpdate, a tracked re-read on the same context
    /// could return stale ColIndex values for any seat already tracked earlier in the same request.</summary>
    Task<List<Seat>> GetSeatMapByRoomNoTrackingAsync(Guid roomId);
    Task<IReadOnlyDictionary<Guid, Seat>> GetByIdsAsync(IReadOnlyCollection<Guid> ids);
    Task<IEnumerable<Guid>> GetBookedSeatIdsAsync(Guid showTimeId, Guid roomId);

    /// <summary>Booked-seat counts for every showtime of a movie, keyed by (ShowTimeId, RoomId).
    /// One query for the whole movie — callers rendering a schedule must not loop per showtime.</summary>
    Task<IReadOnlyDictionary<(Guid ShowTimeId, Guid RoomId), int>> GetBookedSeatCountsByMovieAsync(Guid movieId);

    /// <summary>True if the seat was ever referenced by an invoice ticket (InvoiceTicket.SeatId has an
    /// FK Restrict on Seat), whatever the invoice's status — such a seat can never be hard-deleted.</summary>
    Task<bool> HasAnyTicketAsync(Guid seatId);

    /// <summary>Shifts every seat after afterColIndex in the given room/row one column to the left, in a
    /// single set-based UPDATE (required: the row has a unique (RoomId, RowName, ColIndex) index, so
    /// per-entity updates would collide mid-shift). Returns the number of seats shifted.</summary>
    Task<int> ShiftRowLeftAsync(Guid roomId, string rowName, int afterColIndex);
}
