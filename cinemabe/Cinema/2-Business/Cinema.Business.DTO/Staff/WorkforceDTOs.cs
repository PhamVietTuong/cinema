using System.ComponentModel.DataAnnotations;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Staff;

// ── Roster ───────────────────────────────────────────────────────────────────

public class GetTheaterStaffRequest
{
    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }
}

public class TheaterStaffDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
}

public class RosterRequest
{
    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }
    public DateTime From { get; set; }
    /// <summary>Exclusive end of the range; at most 31 days after <see cref="From"/>.</summary>
    public DateTime To { get; set; }
    public Guid? UserId { get; set; }
}

public class MyShiftsRequest
{
    public DateTime From { get; set; }
    /// <summary>Exclusive end of the range; at most 31 days after <see cref="From"/>.</summary>
    public DateTime To { get; set; }
}

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

public class DeleteStaffShiftRequest
{
    public Guid ShiftId { get; set; }
}

public class StaffShiftDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Note { get; set; }
}

// ── Time clock ───────────────────────────────────────────────────────────────

public class ClockInRequest
{
    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public class ClockOutRequest
{
    [StringLength(500)]
    public string? Note { get; set; }
}

public class TimeSheetRequest
{
    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }
    public DateTime From { get; set; }
    /// <summary>Exclusive end of the range; at most 31 days after <see cref="From"/>.</summary>
    public DateTime To { get; set; }
    public Guid? UserId { get; set; }
}

/// <summary>Instants are UTC.</summary>
public class TimeClockEntryDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime ClockInAt { get; set; }
    public DateTime? ClockOutAt { get; set; }
    /// <summary>Minutes worked; an open entry counts up to now.</summary>
    public int DurationMinutes { get; set; }
    public string? Note { get; set; }
}

public class ClockStatusDTO
{
    public bool IsClockedIn { get; set; }
    /// <summary>The open entry while clocked in.</summary>
    public TimeClockEntryDTO? OpenEntry { get; set; }
}

// ── Tasks ────────────────────────────────────────────────────────────────────

public class SaveStaffTaskRequest
{
    /// <summary>Null creates a task; set it to edit one (including cancelling it).</summary>
    public Guid? Id { get; set; }

    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }
    public Guid AssignedToUserId { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }
    public DateTime? DueAt { get; set; }
    public Guid? IncidentId { get; set; }
    public Guid? ChecklistRunId { get; set; }

    /// <summary>On edit only; a new task always starts Open.</summary>
    public StaffTaskStatus? Status { get; set; }
}

public class MyTasksRequest
{
    /// <summary>Also return Done and Cancelled tasks.</summary>
    public bool IncludeClosed { get; set; }
}

public class SetMyTaskStatusRequest
{
    public Guid TaskId { get; set; }

    /// <summary>Open, InProgress or Done. Cancelling is for approvers (<c>SaveTask</c>).</summary>
    public StaffTaskStatus Status { get; set; }
}

public class StaffTaskDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public Guid AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueAt { get; set; }
    public StaffTaskStatus Status { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? IncidentId { get; set; }
    public Guid? ChecklistRunId { get; set; }
    public DateTime CreationTime { get; set; }
}
