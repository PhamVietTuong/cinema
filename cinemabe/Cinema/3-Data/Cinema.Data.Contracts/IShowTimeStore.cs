using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>Flattened ShowTime x Room row for the movie-detail schedule strip. Projected + untracked:
/// one query, only the columns that page renders.</summary>
public record MovieScheduleRow(
    Guid ShowTimeId,
    DateTime StartTime,
    DateTime EndTime,
    ProjectionForm ProjectionForm,
    Guid RoomId,
    string RoomName,
    string RoomTypeName,
    string TheaterName,
    int Capacity);

public interface IShowTimeStore : IGenericStore<ShowTime>
{
    Task<IEnumerable<ShowTime>> GetByMovieAndDateAsync(Guid movieId, Guid theaterId, DateOnly date);
    /// <summary>Flattened ShowTime x Room schedule of one movie inside [fromInclusive, toExclusive),
    /// ordered by StartTime. Backs the movie-detail date-tab strip (today + next 3 days).</summary>
    Task<IReadOnlyList<MovieScheduleRow>> GetMovieScheduleAsync(Guid movieId, DateTime fromInclusive, DateTime toExclusive);
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
