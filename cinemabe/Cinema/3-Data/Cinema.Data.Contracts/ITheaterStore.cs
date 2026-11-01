using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface ITheaterStore : IGenericStore<Theater>
{
    /// <summary>Active theaters matching the optional name/city filters, filtered, counted and paged
    /// in SQL (untracked). <paramref name="pageSize"/> &lt;= 0 returns every match, unpaged.</summary>
    Task<(IReadOnlyList<(Theater Theater, int RoomCount)> Items, int Total)> SearchAsync(
        string? name, string? city, int page, int pageSize);
    Task<Theater?> GetDetailAsync(Guid id);
    Task<IEnumerable<Theater>> GetByMovieAsync(Guid movieId, DateTime date);

    /// <summary>Theater name by id for a batch of ids (one query). Unknown ids are absent.</summary>
    Task<Dictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids);
}
