using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

/// <summary>Composite-key store for the Movie ↔ MovieType join table.</summary>
public interface IMovieTypeDetailStore
{
    Task<IEnumerable<MovieTypeDetail>> GetAllAsync();
    Task<bool> ExistsAsync(Guid movieId, Guid movieTypeId);
    Task AddAsync(MovieTypeDetail entity);
    Task DeleteAsync(Guid movieId, Guid movieTypeId);
}
