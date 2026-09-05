namespace Cinema.Business.DTO.Movies;
public class MovieDetailDTO : MovieDTO
{
    public string AgeRestrictionDescription { get; set; } = string.Empty;
    public int AgeRestrictionMinAge { get; set; }
    /// <summary>Upcoming screenings within today + the next 3 days only (not every future
    /// screening) — the movie-detail page's date tabs slice this window client-side.</summary>
    public List<ShowTimeSummaryDTO> ShowTimes { get; set; } = new();
    public List<CommentDTO> RecentComments { get; set; } = new();
}
