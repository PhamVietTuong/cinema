using System.ComponentModel.DataAnnotations;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Operations;

// ── Incidents ────────────────────────────────────────────────────────────────

public class ReportIncidentRequest
{
    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }
    public IncidentCategory Category { get; set; }
    public IncidentSeverity Severity { get; set; } = IncidentSeverity.Medium;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }
    public Guid? RoomId { get; set; }
    public Guid? SeatId { get; set; }
    public Guid? ShowTimeId { get; set; }
}
