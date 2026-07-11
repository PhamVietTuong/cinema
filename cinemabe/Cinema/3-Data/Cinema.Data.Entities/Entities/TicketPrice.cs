namespace Cinema.Data.Entities;

/// <summary>
/// An explicit ticket price for a (theater, seat type, time slot, holiday?) combination.
/// The full price of a ticket is read directly from the matching row.
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

    public double Price { get; set; }
}
