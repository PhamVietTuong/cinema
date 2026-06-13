using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IShowTimeStore : IGenericStore<ShowTime>
{
    Task<IEnumerable<ShowTime>> GetByMovieAndDateAsync(Guid movieId, Guid theaterId, DateOnly date);
    Task<ShowTimeRoom?> GetShowTimeRoomAsync(Guid showTimeId, Guid roomId);
    Task<IEnumerable<ShowTime>> GetAllWithRoomsAsync();
    Task<ShowTime?> GetByIdWithRoomsAsync(Guid id);
    /// <summary>Assigns a single room (replacing any existing assignments) to a showtime.</summary>
    Task SetRoomAsync(Guid showTimeId, Guid roomId, int basePrice);
}
