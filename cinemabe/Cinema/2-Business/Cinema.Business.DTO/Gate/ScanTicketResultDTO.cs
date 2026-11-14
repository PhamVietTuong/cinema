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
    /// <summary>True for rating K (under 13 admitted only with a parent or guardian); the gate shows a banner.</summary>
    public bool GuardianRequired { get; set; }
    /// <summary>True when the ticket was sold under an ID-check patron category (student, senior); the gate shows a
    /// non-blocking reminder to check the holder's ID. Set for every outcome that identifies a ticket.</summary>
    public bool RequiresIdCheck { get; set; }
    /// <summary>For AlreadyUsed: when the ticket was admitted (UTC).</summary>
    public DateTime? UsedAt { get; set; }
    /// <summary>For AlreadyUsed: the name of the staff member who admitted it.</summary>
    public string? UsedBy { get; set; }
}
