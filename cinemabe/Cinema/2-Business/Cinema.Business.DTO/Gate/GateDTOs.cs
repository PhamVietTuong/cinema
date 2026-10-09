using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Gate;

/// <summary>The decision of a gate scan. Every value other than <see cref="Admitted"/> means "do not let in".</summary>
public enum ScanOutcome
{
    Admitted = 0,
    NotFound = 1,
    NotPaid = 2,
    AlreadyUsed = 3,
    WrongTheater = 4,
    WrongShowTime = 5,
    TooEarly = 6,
    Expired = 7,
    /// <summary>The movie is age restricted: the gate must check ID, then re-scan with AgeConfirmed = true.
    /// The ticket is NOT marked used.</summary>
    AgeCheckRequired = 8
}

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
