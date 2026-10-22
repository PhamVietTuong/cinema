
namespace Cinema.Business.DTO.Catalog;

/// <summary>Batch create-or-update: applies one multiplier to a (theater, room type, time slot) across
/// several days of week in one call, used by the weekly pricing timeline's cell editor.</summary>
public class UpsertTicketPricesRequest
{
    public Guid TheaterId { get; set; }
    public Guid RoomTypeId { get; set; }
    public Guid TimeSlotId { get; set; }
    public List<DayOfWeek> DaysOfWeek { get; set; } = new();
    /// <summary>Factor applied to the resolved PatronCategory price, not an absolute amount.</summary>
    public double PriceMultiplier { get; set; } = 1;
}
