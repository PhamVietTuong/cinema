
namespace Cinema.Business.DTO.Catalog;

/// <summary>Replaces one holiday's per-slot price factors for a single theater in one call. Each item
/// names a time slot of <see cref="TheaterId"/>; a null <see cref="HolidayTimeSlotPriceItem.PriceMultiplier"/>
/// removes that slot's override so it falls back to the holiday's default factor. Slots of the theater
/// that are not listed are left untouched.</summary>
public class UpsertHolidayTimeSlotPricesRequest
{
    public Guid HolidayId { get; set; }
    public Guid TheaterId { get; set; }
    public List<HolidayTimeSlotPriceItem> Items { get; set; } = new();
}

public class HolidayTimeSlotPriceItem
{
    public Guid TimeSlotId { get; set; }

    /// <summary>Factor applied to the resolved PatronCategory price; null clears the override.</summary>
    public double? PriceMultiplier { get; set; }
}
