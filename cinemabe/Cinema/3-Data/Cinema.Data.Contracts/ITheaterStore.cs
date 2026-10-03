using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface ITheaterStore : IGenericStore<Theater>
{
    Task<IEnumerable<Theater>> GetTheatersWithRoomsAsync();
    Task<Theater?> GetDetailAsync(Guid id);
    Task<IEnumerable<Theater>> GetByMovieAsync(Guid movieId, DateTime date);

    /// <summary>Theater name by id for a batch of ids (one query). Unknown ids are absent.</summary>
    Task<Dictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids);
}
