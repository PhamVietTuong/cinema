using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class SeatTypeDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#808080";
    public double PriceMultiplier { get; set; } = 1;
}

public class CreateSeatTypeRequest
{
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#808080";
    public double PriceMultiplier { get; set; } = 1;
}

public class UpdateSeatTypeRequest : IHasId
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#808080";
    public double PriceMultiplier { get; set; } = 1;
}
