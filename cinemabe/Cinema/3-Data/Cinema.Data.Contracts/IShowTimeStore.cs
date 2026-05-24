using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IShowTimeStore : IGenericStore<ShowTime>
{
    Task<IEnumerable<ShowTime>> GetByMovieAndDateAsync(Guid movieId, Guid theaterId, DateOnly date);
    Task<ShowTimeRoom?> GetShowTimeRoomAsync(Guid showTimeId, Guid roomId);
}
