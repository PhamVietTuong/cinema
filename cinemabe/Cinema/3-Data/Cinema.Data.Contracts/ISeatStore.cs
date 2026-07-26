using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface ISeatStore : IGenericStore<Seat>
{
    Task<IEnumerable<Seat>> GetByRoomAsync(Guid roomId);
    Task<IEnumerable<Guid>> GetBookedSeatIdsAsync(Guid showTimeId, Guid roomId);

    /// <summary>Booked-seat counts for every showtime of a movie, keyed by (ShowTimeId, RoomId).
    /// One query for the whole movie — callers rendering a schedule must not loop per showtime.</summary>
    Task<IReadOnlyDictionary<(Guid ShowTimeId, Guid RoomId), int>> GetBookedSeatCountsByMovieAsync(Guid movieId);
}
