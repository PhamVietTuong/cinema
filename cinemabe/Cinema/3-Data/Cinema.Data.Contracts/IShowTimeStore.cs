using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IShowTimeStore : IGenericStore<ShowTime>
{
    Task<IEnumerable<ShowTime>> GetByMovieAndDateAsync(Guid movieId, Guid theaterId, DateOnly date);
    Task<ShowTimeRoom?> GetShowTimeRoomAsync(Guid showTimeId, Guid roomId);
    /// <summary>Filtered, DB-side paged showtime list (with room + theater eager-loaded).</summary>
    Task<(IReadOnlyList<ShowTime> Items, int Total)> SearchAsync(
        Guid? movieId, Guid? roomId, bool? isActive, int page, int pageSize);
    Task<ShowTime?> GetByIdWithRoomsAsync(Guid id);
}
