namespace Cinema.Data.Entities;

/// <summary>
/// A pricing factor for a (theater, room type, seat type, time slot, holiday?) combination.
/// Applied to the showtime's BasePrice — mirrors SeatType.PriceMultiplier and Holiday.PriceMultiplier
/// rather than replacing BasePrice outright, so a movie-specific base price is never undercut.
/// </summary>
public class TicketPrice : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Theater Theater { get; set; } = null!;

    public Guid RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    public Guid SeatTypeId { get; set; }
    public SeatType SeatType { get; set; } = null!;

    public Guid TimeSlotId { get; set; }
    public TimeSlot TimeSlot { get; set; } = null!;

    /// <summary>Whether this price applies on holidays (true) or on normal days (false).</summary>
    public bool IsHoliday { get; set; }

    public double PriceMultiplier { get; set; } = 1;
}
