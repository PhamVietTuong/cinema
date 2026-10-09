using Cinema.Data.Enums;

namespace Cinema.Data.Entities;

/// <summary>
/// A problem reported by staff in a theater (broken seat, projector fault, ...). No foreign keys on purpose,
/// like <see cref="AuditLog"/>: the history survives deletion of the room/seat/user it mentions.
/// </summary>
public class Incident : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Guid? RoomId { get; set; }
    public Guid? SeatId { get; set; }
    public Guid? ShowTimeId { get; set; }
    public IncidentCategory Category { get; set; } = IncidentCategory.Other;
    public IncidentSeverity Severity { get; set; } = IncidentSeverity.Medium;
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ReportedByUserId { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
    /// <summary>True while this incident took <see cref="SeatId"/> (and its double-seat partner) out of sale.</summary>
    public bool BlocksSeat { get; set; }
    /// <summary>True while this incident put <see cref="RoomId"/> into maintenance.</summary>
    public bool BlocksRoom { get; set; }
}
