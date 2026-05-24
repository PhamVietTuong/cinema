using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface ITheaterStore : IGenericStore<Theater>
{
    Task<IEnumerable<Theater>> GetTheatersWithRoomsAsync();
    Task<Theater?> GetDetailAsync(Guid id);
    Task<IEnumerable<Theater>> GetByMovieAsync(Guid movieId, DateTime date);
}
