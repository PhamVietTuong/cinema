using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>Read-only projection of one ticket with everything the gate needs to decide a scan.</summary>
public sealed class GateTicketRow
{
    public Guid InvoiceId { get; set; }
    public Guid ShowTimeId { get; set; }
    public Guid SeatId { get; set; }
    public bool IsUsed { get; set; }
    public bool IsActive { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedByName { get; set; }
    public string? PatronCategoryName { get; set; }
    public InvoiceStatus InvoiceStatus { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public string SeatLabel { get; set; } = string.Empty;
    public string MovieTitle { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public Guid TheaterId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? AgeRatingCode { get; set; }
    public int? MinAge { get; set; }
}
