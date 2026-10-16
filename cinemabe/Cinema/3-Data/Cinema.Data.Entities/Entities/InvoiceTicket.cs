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
    /// <summary>When the ticket was admitted at the gate (UTC); set atomically together with IsUsed.</summary>
    public DateTime? UsedAt { get; set; }
    /// <summary>The staff user who admitted the ticket (no FK, like other audit snapshots).</summary>
    public Guid? UsedByUserId { get; set; }
    /// <summary>Whether this ticket still holds its seat. True while the booking is Pending/Paid; set false
    /// on cancel/expire. A filtered unique index over active (ShowTimeId, RoomId, SeatId) rows gives a
    /// DB-level guarantee that two instances can't double-book the same seat.</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>Seat label frozen at sale time (row-position numbering: a double seat counts as one slot).
    /// Written once by BookingManager.ComposeInvoiceAsync; source of truth for every customer-facing display
    /// of a sold ticket. Never recomputed, even if the room's seats are later paired/resized/removed. Null
    /// for rows written before this column existed. Ops views (gate/incident) deliberately ignore this and
    /// keep showing the seat's live physical RowName+ColIndex.</summary>
    public string? SeatLabel { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public ShowTimeRoom ShowTimeRoom { get; set; } = null!;
    public Seat Seat { get; set; } = null!;
}
