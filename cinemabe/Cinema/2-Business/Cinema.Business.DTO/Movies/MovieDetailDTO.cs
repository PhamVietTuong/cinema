namespace Cinema.Business.DTO.Movies;
public class MovieDetailDTO : MovieDTO
{
    public string AgeRestrictionDescription { get; set; } = string.Empty;
    public int AgeRestrictionMinAge { get; set; }
    /// <summary>Upcoming screenings within today + the next 3 days only (not every future
    /// screening) — the movie-detail page's date tabs slice this window client-side.</summary>
    public List<ShowTimeSummaryDTO> ShowTimes { get; set; } = new();
    public List<CommentDTO> RecentComments { get; set; } = new();
    /// <summary>True when the requesting user has watched the movie (paid, active ticket for a finished
    /// screening) and may rate/comment. Always false for anonymous callers.</summary>
    public bool CanReview { get; set; }
    /// <summary>Explicit Director/Actor credits, ordered Directors then Actors then DisplayOrder.
    /// Empty means this movie has none set up yet; the client then falls back to splitting the
    /// Director/Cast strings.</summary>
    public List<MovieCreditDTO> Credits { get; set; } = new();
    /// <summary>Third-party review scores (OMDb, matched by title and release year). Null when no
    /// OMDb API key is configured, the movie isn't found on OMDb, or OMDb couldn't be reached —
    /// the client hides the section in each of those cases.</summary>
    public ExternalRatingsDTO? ExternalRatings { get; set; }
}
