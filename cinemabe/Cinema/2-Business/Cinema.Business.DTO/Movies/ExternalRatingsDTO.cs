namespace Cinema.Business.DTO.Movies;

/// <summary>Third-party review scores (OMDb, matched by title and release year). Every property is
/// a display-ready string already formatted by the source (e.g. "93%", "8.1", "74") or null when
/// that particular score wasn't returned.</summary>
public class ExternalRatingsDTO
{
    /// <summary>Rotten Tomatoes critic score as OMDb reports it, e.g. "93%". Null when OMDb has no
    /// Rotten Tomatoes entry for this title.</summary>
    public string? RottenTomatoes { get; set; }
    /// <summary>IMDb rating out of 10, e.g. "8.1".</summary>
    public string? ImdbRating { get; set; }
    /// <summary>Metacritic score out of 100, e.g. "74".</summary>
    public string? Metascore { get; set; }
}
