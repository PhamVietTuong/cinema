using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Movies;

/// <summary>One hit of the public cross-movie showtime search: a showtime in a room, with its movie and theater.</summary>
public class ShowTimeSearchResultDTO
{
    public Guid           ShowTimeId     { get; set; }
    public Guid           RoomId         { get; set; }
    public DateTime       StartTime      { get; set; }
    public DateTime       EndTime        { get; set; }
    public ProjectionForm ProjectionForm { get; set; }
    public Guid           MovieId        { get; set; }
    public string         MovieTitle     { get; set; } = string.Empty;
    public string         MovieSlug      { get; set; } = string.Empty;
    public string?        PosterUrl      { get; set; }
    public string?        Language       { get; set; }
    public Guid           TheaterId      { get; set; }
    public string         TheaterName    { get; set; } = string.Empty;
    public string         RoomName       { get; set; } = string.Empty;
    public string         RoomTypeName   { get; set; } = string.Empty;
    public double         BasePrice      { get; set; }
}
