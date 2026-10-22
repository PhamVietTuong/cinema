namespace Cinema.Data.Contracts;

/// <summary>Read-only queries behind the staff schedule board.</summary>
public interface IScheduleBoardStore
{
    /// <summary>Every room of the theater, with class buffer and active-seat capacity (one query).</summary>
    Task<List<BoardRoomRow>> GetRoomsAsync(Guid theaterId);

    /// <summary>Active showtimes of the theater starting in [from, to), ordered by start (one query).</summary>
    Task<List<BoardShowTimeRow>> GetShowTimesAsync(Guid theaterId, DateTime from, DateTime to);

    /// <summary>Sold seats (active Pending/Paid tickets) per (ShowTimeId, RoomId) for showtimes starting in [from, to): one grouped query.</summary>
    Task<IReadOnlyDictionary<(Guid ShowTimeId, Guid RoomId), int>> GetSoldCountsAsync(Guid theaterId, DateTime from, DateTime to);
}
