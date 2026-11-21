namespace Cinema.Data.Entities;

/// <summary>
/// A per-time-slot price factor for one <see cref="Holiday"/>. A holiday still carries a single
/// <see cref="Holiday.PriceMultiplier"/> as its default; a row here overrides that default for showtimes
/// that start inside <see cref="TimeSlot"/> on that holiday's date, so a holiday can price the morning,
/// evening and late-night slots differently. <see cref="TimeSlot"/> is per theater, so a row is
/// implicitly scoped to that slot's theater. Like the weekday matrix, the factor multiplies the resolved
/// PatronCategory price and is never stacked on top of a <see cref="TicketPrice"/> row.
/// </summary>
public class HolidayTimeSlotPrice : BaseEntity
{
    public Guid HolidayId { get; set; }
    public Holiday Holiday { get; set; } = null!;

    public Guid TimeSlotId { get; set; }
    public TimeSlot TimeSlot { get; set; } = null!;

    public double PriceMultiplier { get; set; } = 1;
}
