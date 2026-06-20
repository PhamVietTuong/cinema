namespace Cinema.Business.DTO.Catalog;

// ── MovieTypeDetail (Movie ↔ MovieType) ───────────────────────────────────────
public class MovieTypeDetailDTO
{
    public Guid MovieId { get; set; }
    public Guid MovieTypeId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string MovieTypeName { get; set; } = string.Empty;
}

public class CreateMovieTypeDetailRequest
{
    public Guid MovieId { get; set; }
    public Guid MovieTypeId { get; set; }
}
