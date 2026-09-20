using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Movies;

public class UpdateMovieRequest
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    [Range(1, 600, ErrorMessage = "Duration must be between 1 and 600 minutes.")]
    public int Duration { get; set; }
    public DateOnly ReleaseDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? PosterUrl { get; set; }
    public string? TrailerUrl { get; set; }
    public string? Director { get; set; }
    public string? Cast { get; set; }
    public string? Language { get; set; }
    public string? Subtitle { get; set; }
    public Guid AgeRestrictionId { get; set; }
    public List<Guid> MovieTypeIds { get; set; } = new();
}
