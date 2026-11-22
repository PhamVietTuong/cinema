using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

/// <summary>Composite-key store for the Movie ↔ MovieType join table.</summary>
public interface IMovieTypeDetailStore
{
    Task<IEnumerable<MovieTypeDetail>> GetAllAsync();
    /// <summary>One page of pairings with movie title and type name, projected and paged in SQL, plus the total count.</summary>
    Task<(IReadOnlyList<MovieTypeDetailRow> Items, int Total)> GetPagedAsync(int page, int pageSize);
    Task<bool> ExistsAsync(Guid movieId, Guid movieTypeId);
    Task AddAsync(MovieTypeDetail entity);
    Task DeleteAsync(Guid movieId, Guid movieTypeId);
}

/// <summary>Projected Movie ↔ MovieType pairing for the admin list.</summary>
public record MovieTypeDetailRow(Guid MovieId, Guid MovieTypeId, string MovieTitle, string MovieTypeName);
