using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IShowTimeStore : IGenericStore<ShowTime>
{
    Task<IEnumerable<ShowTime>> GetByMovieAndDateAsync(Guid movieId, Guid theaterId, DateOnly date);
    Task<ShowTimeRoom?> GetShowTimeRoomAsync(Guid showTimeId, Guid roomId);
    /// <summary>Filtered, DB-side paged showtime list (with room + theater eager-loaded).
    /// <paramref name="from"/>/<paramref name="to"/> bound StartTime as a half-open range: from &lt;= StartTime &lt; to.</summary>
    Task<(IReadOnlyList<ShowTime> Items, int Total)> SearchAsync(
        Guid? movieId, Guid? roomId, bool? isActive, DateTime? from, DateTime? to, int page, int pageSize);
    Task<ShowTime?> GetByIdWithRoomsAsync(Guid id);
    /// <summary>True if an active showtime already occupies <paramref name="roomId"/> for any part of
    /// [startTime, endTime]. Pass an id in <paramref name="excludeShowTimeId"/> to ignore the row being edited.</summary>
    Task<bool> HasRoomOverlapAsync(Guid roomId, DateTime startTime, DateTime endTime, Guid? excludeShowTimeId);
}
