
namespace Cinema.Business.DTO.Catalog;

public class TicketPriceDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public Guid RoomTypeId { get; set; }
    public Guid TimeSlotId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    /// <summary>Factor applied to the resolved PatronCategory price, not an absolute amount.</summary>
    public double PriceMultiplier { get; set; } = 1;
}
