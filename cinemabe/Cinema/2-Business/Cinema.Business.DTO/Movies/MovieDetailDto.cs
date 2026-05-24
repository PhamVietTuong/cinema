namespace Cinema.Business.DTO.Movies;
public class MovieDetailDTO : MovieDTO
{
    public string AgeRestrictionDescription { get; set; } = string.Empty;
    public int AgeRestrictionMinAge { get; set; }
    public List<ShowTimeSummaryDTO> ShowTimes { get; set; } = new();
    public List<CommentDTO> RecentComments { get; set; } = new();
}
