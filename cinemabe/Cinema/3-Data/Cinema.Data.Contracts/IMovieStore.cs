using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IMovieStore : IGenericStore<Movie>
{
    Task<(IEnumerable<Movie> Items, int Total)> GetPagedAsync(string? search, Guid? movieTypeId, int page, int pageSize);
    Task<Movie?> GetDetailAsync(Guid id);
    Task<Movie?> GetForUpdateAsync(Guid id);
    Task<IEnumerable<Movie>> GetNowShowingAsync();
    Task<IEnumerable<Movie>> GetComingSoonAsync();
    Task<double> GetAverageRatingAsync(Guid movieId);
}
