namespace Cinema.Business.DTO.Movies;
public class MovieDTO
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Duration { get; set; }
    public DateOnly ReleaseDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? PosterUrl { get; set; }
    public string? TrailerUrl { get; set; }
    public string? Director { get; set; }
    public string? Cast { get; set; }
    public string? Language { get; set; }
    public string AgeRestrictionCode { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    public bool IsActive { get; set; }
    public bool IsNowShowing { get; set; }
    public bool IsComingSoon { get; set; }
}
