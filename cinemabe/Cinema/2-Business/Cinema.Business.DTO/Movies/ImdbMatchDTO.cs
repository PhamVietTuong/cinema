namespace Cinema.Business.DTO.Movies;

/// <summary>One candidate returned by the admin "find IMDb id" lookup (OMDb title search). Used to
/// auto-fill <c>Movie.ImdbId</c> in the movie dialog.</summary>
public class ImdbMatchDTO
{
    /// <summary>IMDb id, e.g. "tt0448694".</summary>
    public string ImdbId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>Release year as OMDb reports it; may be a range for series (e.g. "2011–2013").</summary>
    public string Year { get; set; } = string.Empty;
    /// <summary>Poster URL, or null when OMDb has none.</summary>
    public string? PosterUrl { get; set; }
}
