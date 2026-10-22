using Cinema.Data.Entities;

namespace Cinema.Business.DTO.Catalog;

public class SeatTypeDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public SeatKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#808080";
}
