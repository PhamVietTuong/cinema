using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Staff;

public class SaveStaffShiftRequest
{
    /// <summary>Null creates a shift; set it to edit one.</summary>
    public Guid? Id { get; set; }

    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}
