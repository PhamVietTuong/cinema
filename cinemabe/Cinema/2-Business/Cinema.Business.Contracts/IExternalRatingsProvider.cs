using Cinema.Business.DTO.Movies;

namespace Cinema.Business.Contracts;

/// <summary>Looks up third-party review scores for a movie, by IMDb id when one is supplied, otherwise
/// by title and release year. Never throws for upstream problems (missing config, not found,
/// network/parse failure) — returns null instead, so a caller can always treat "no ratings" and
/// "ratings unavailable" the same way.</summary>
public interface IExternalRatingsProvider
{
    Task<ExternalRatingsDTO?> GetRatingsAsync(string title, int year, string? imdbId, CancellationToken cancellationToken = default);
}
