using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

/// <summary>An active staff member of a theater, for the roster picker.</summary>
public record TheaterStaffRow(Guid Id, string Name, string RoleName);

/// <summary>
/// Rosters. Methods that return tracked entities say so: edit them, then call
/// <c>IApplicationUnitOfWork.SaveChangesAsync</c>. <c>Stage*</c> methods only track; the caller saves.
/// </summary>
public interface IWorkforceStore
{
    // ── Roster ───────────────────────────────────────────────────────────────

    /// <summary>Shifts of a theater overlapping [from, to), optionally of one user, ordered by start, untracked.</summary>
    Task<List<StaffShift>> GetShiftsAsync(Guid theaterId, DateTime from, DateTime to, Guid? userId);

    /// <summary>Shifts of one user overlapping [from, to) across theaters, ordered by start, untracked.</summary>
    Task<List<StaffShift>> GetUserShiftsAsync(Guid userId, DateTime from, DateTime to);

    /// <summary>One shift, TRACKED.</summary>
    Task<StaffShift?> GetShiftAsync(Guid id);

    /// <summary>True when the user already has a shift overlapping [start, end) (other than <paramref name="excludeShiftId"/>).</summary>
    Task<bool> HasShiftOverlapAsync(Guid userId, DateTime start, DateTime end, Guid? excludeShiftId);

    void StageShift(StaffShift shift);
    void StageDeleteShift(StaffShift shift);

    /// <summary>Active theater-scoped staff accounts of a theater (one projected query).</summary>
    Task<List<TheaterStaffRow>> GetTheaterStaffAsync(Guid theaterId, IReadOnlyCollection<string> roleNames);
}
