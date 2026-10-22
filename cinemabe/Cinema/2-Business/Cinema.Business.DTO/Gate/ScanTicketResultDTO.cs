using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Gate;

public class ScanTicketResultDTO
{
    public ScanOutcome Outcome { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public string SeatLabel { get; set; } = string.Empty;
    public string MovieTitle { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DateTime ShowTime { get; set; }
    public string PatronCategory { get; set; } = string.Empty;
    public string? AgeRatingCode { get; set; }
    public int? MinAge { get; set; }
    /// <summary>For AlreadyUsed: when the ticket was admitted (UTC).</summary>
    public DateTime? UsedAt { get; set; }
    /// <summary>For AlreadyUsed: the name of the staff member who admitted it.</summary>
    public string? UsedBy { get; set; }
}
