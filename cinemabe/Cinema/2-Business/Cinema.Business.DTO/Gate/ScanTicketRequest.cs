using System.ComponentModel.DataAnnotations;

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
