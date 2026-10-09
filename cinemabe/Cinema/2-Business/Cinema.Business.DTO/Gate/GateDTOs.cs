using System.ComponentModel.DataAnnotations;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Gate;

public class ScanTicketRequest
{
    [Required]
    [StringLength(500)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Theater being guarded. Optional for single-theater staff; required for admins.</summary>
    public Guid? TheaterId { get; set; }

    /// <summary>Optional filter: when set, tickets for any other showtime are rejected with WrongShowTime.</summary>
    public Guid? ShowTimeId { get; set; }

    /// <summary>True once the gate staff has checked the patron's ID for an age-restricted movie.</summary>
    public bool AgeConfirmed { get; set; }
}

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

public class GateLookupRequest
{
    [StringLength(50)]
    public string? InvoiceCode { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    public Guid? TheaterId { get; set; }
}

public class GateLookupTicketDTO
{
    public string QrCode { get; set; } = string.Empty;
    public string SeatLabel { get; set; } = string.Empty;
    public string MovieTitle { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DateTime ShowTime { get; set; }
    public bool IsUsed { get; set; }
}

public class GateLookupResultDTO
{
    public string InvoiceCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    /// <summary>Phone with all but the last 3 digits masked.</summary>
    public string MaskedPhone { get; set; } = string.Empty;
    public List<GateLookupTicketDTO> Tickets { get; set; } = new();
}
