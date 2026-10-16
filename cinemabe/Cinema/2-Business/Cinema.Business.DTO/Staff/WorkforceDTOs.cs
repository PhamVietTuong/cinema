using System.ComponentModel.DataAnnotations;

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

