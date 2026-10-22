namespace Cinema.Data.Contracts;

/// <summary>A showtime screening in a room, with its movie title.</summary>
public record BoardShowTimeRow(Guid ShowTimeId, Guid RoomId, Guid MovieId, string MovieTitle, DateTime StartTime, DateTime EndTime);
