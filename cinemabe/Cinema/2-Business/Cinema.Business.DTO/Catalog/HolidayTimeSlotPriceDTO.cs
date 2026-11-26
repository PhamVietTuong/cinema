
namespace Cinema.Business.DTO.Catalog;

/// <summary>A holiday's price factor for one theater time slot; overrides the holiday's default
/// <see cref="HolidayDTO.PriceMultiplier"/> for showtimes starting inside that slot.</summary>
public class HolidayTimeSlotPriceDTO
{
    public Guid Id { get; set; }
    public Guid HolidayId { get; set; }
    public Guid TimeSlotId { get; set; }
    public double PriceMultiplier { get; set; }
}
