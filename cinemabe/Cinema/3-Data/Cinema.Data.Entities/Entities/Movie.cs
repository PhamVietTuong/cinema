namespace Cinema.Data.Entities;
public class Movie : BaseEntity
{
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
    public string? Subtitle { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid AgeRestrictionId { get; set; }
    public AgeRestriction AgeRestriction { get; set; } = null!;
    public ICollection<MovieTypeDetail> MovieTypeDetails { get; set; } = new List<MovieTypeDetail>();
    public ICollection<ShowTime> ShowTimes { get; set; } = new List<ShowTime>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Evaluation> Evaluations { get; set; } = new List<Evaluation>();
}
