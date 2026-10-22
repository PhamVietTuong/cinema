namespace Cinema.Data.Contracts;

/// <summary>Showtime-room screenings whose local start lies in [From, To), optionally limited to theaters.</summary>
public record OccupancyQuery(DateTime FromLocal, DateTime ToLocal, IReadOnlyCollection<Guid>? TheaterIds);
