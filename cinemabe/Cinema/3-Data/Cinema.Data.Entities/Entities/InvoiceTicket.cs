namespace Cinema.Data.Entities;
public class InvoiceTicket
{
    public Guid InvoiceId { get; set; }
    public Guid ShowTimeId { get; set; }
    public Guid RoomId { get; set; }
    public Guid SeatId { get; set; }
    public double Price { get; set; }
    /// <summary>Snapshot of the patron category applied at booking time (no FK, like Invoice.GiftCardId) —
    /// stays truthful for reprints/gate check-in/reports even if the category is later renamed or deleted.</summary>
    public Guid? PatronCategoryId { get; set; }
    public string? PatronCategoryName { get; set; }
    public double PatronDiscountPercent { get; set; }
    public string? QrCode { get; set; }
    public bool IsUsed { get; set; } = false;
    /// <summary>Whether this ticket still holds its seat. True while the booking is Pending/Paid; set false
    /// on cancel/expire/refund. A filtered unique index over active (ShowTimeId, RoomId, SeatId) rows gives a
    /// DB-level guarantee that two instances can't double-book the same seat.</summary>
    public bool IsActive { get; set; } = true;
    public Invoice Invoice { get; set; } = null!;
    public ShowTimeRoom ShowTimeRoom { get; set; } = null!;
    public Seat Seat { get; set; } = null!;
}
