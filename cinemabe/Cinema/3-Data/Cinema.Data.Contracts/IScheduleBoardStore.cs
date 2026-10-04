using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>A room of the theater with its class buffer and its sellable (active) seat count.</summary>
public record BoardRoomRow(Guid RoomId, string RoomName, string RoomTypeName, RoomStatus Status, int TurnoverBufferMinutes, int Capacity);

/// <summary>A showtime screening in a room, with its movie title.</summary>
public record BoardShowTimeRow(Guid ShowTimeId, Guid RoomId, Guid MovieId, string MovieTitle, DateTime StartTime, DateTime EndTime);

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
