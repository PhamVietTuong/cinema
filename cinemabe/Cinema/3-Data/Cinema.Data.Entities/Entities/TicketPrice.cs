namespace Cinema.Data.Entities;

/// <summary>
/// A time-of-day/day-of-week pricing factor for a (theater, room type, time slot, day of week) combination.
/// Multiplies the resolved PatronCategory price (see BookingManager's pricing formula) — this table no
/// longer carries a seat-kind dimension, since PatronCategory now owns the per-seat-kind price.
/// Holiday pricing is not stored here: it comes from <see cref="Holiday"/>, matched by calendar date, and
/// overrides whatever this table says for that date.
/// </summary>
public class TicketPrice : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Theater Theater { get; set; } = null!;

    public Guid RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    public Guid TimeSlotId { get; set; }
    public TimeSlot TimeSlot { get; set; } = null!;

    /// <summary>Weekday this factor applies to; holidays are priced via <see cref="Holiday"/>, not here.</summary>
    public DayOfWeek DayOfWeek { get; set; }

    public double PriceMultiplier { get; set; } = 1;
}
