using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

/// <summary>SeatType rows are fixed (exactly Single + Double per theater, seeded on theater
/// creation) — only Name/Description/Color can be edited, never Kind, and there is no create/delete.</summary>
public class UpdateSeatTypeRequest : IHasId
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#808080";
}
