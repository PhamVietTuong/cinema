
namespace Cinema.Business.DTO.Catalog;

public class HolidayDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public double PriceMultiplier { get; set; }
}
