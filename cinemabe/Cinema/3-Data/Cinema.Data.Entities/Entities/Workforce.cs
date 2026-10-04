using Cinema.Data.Enums;

namespace Cinema.Data.Entities;

/// <summary>A rostered working period of one staff member in a theater. Times are the theater's local wall-clock times (like showtimes).</summary>
public class StaffShift : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// A clock-in/out pair. <see cref="ClockOutAt"/> is null while the person is clocked in; a filtered unique index
/// allows only one such open entry per user. Instants are UTC. Clocking in is not required to sell (decision D8):
/// the time clock is reporting only.
/// </summary>
public class TimeClockEntry : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ClockInAt { get; set; }
    public DateTime? ClockOutAt { get; set; }
    public string? Note { get; set; }
}

/// <summary>A to-do assigned to one staff member, optionally tied to an incident or a checklist run. No FKs, like <see cref="Incident"/>.</summary>
public class StaffTask : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Guid AssignedToUserId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueAt { get; set; }
    public StaffTaskStatus Status { get; set; } = StaffTaskStatus.Open;
    public DateTime? CompletedAt { get; set; }
    public Guid? IncidentId { get; set; }
    public Guid? ChecklistRunId { get; set; }
}
