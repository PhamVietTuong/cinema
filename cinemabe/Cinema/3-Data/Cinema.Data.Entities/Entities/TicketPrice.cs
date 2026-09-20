namespace Cinema.Data.Entities;

/// <summary>
/// A time-of-day/holiday pricing factor for a (theater, room type, time slot, holiday?) combination.
/// Multiplies the resolved PatronCategory price (see BookingManager's pricing formula) — this table no
/// longer carries a seat-kind dimension, since PatronCategory now owns the per-seat-kind price.
/// </summary>
public class TicketPrice : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Theater Theater { get; set; } = null!;

    public Guid RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    public Guid TimeSlotId { get; set; }
    public TimeSlot TimeSlot { get; set; } = null!;

    /// <summary>Whether this price applies on holidays (true) or on normal days (false).</summary>
    public bool IsHoliday { get; set; }

    public double PriceMultiplier { get; set; } = 1;
}
