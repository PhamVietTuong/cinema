using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IMovieStore : IGenericStore<Movie>
{
    Task<(IEnumerable<Movie> Items, int Total)> GetPagedAsync(string? search, string? director, Guid? movieTypeId, int page, int pageSize);
    Task<Movie?> GetDetailAsync(Guid id);
    Task<Movie?> GetForUpdateAsync(Guid id);
    Task<IEnumerable<Movie>> GetNowShowingAsync();
    Task<IEnumerable<Movie>> GetComingSoonAsync();
    Task<double> GetAverageRatingAsync(Guid movieId);

    /// <summary>Average rating for many movies in one grouped query. Movies with no ratings are absent
    /// from the result. Use this instead of calling <see cref="GetAverageRatingAsync"/> in a loop.</summary>
    Task<IReadOnlyDictionary<Guid, double>> GetAverageRatingsAsync(IReadOnlyCollection<Guid> movieIds);
}
