using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class UpdateHolidayRequest : IHasId
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public double PriceMultiplier { get; set; } = 1.5;
}
