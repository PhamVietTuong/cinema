using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>An active staff member of a theater, for the roster picker.</summary>
public record TheaterStaffRow(Guid Id, string Name, string RoleName);

/// <summary>Filter + page for tasks (newest first). PageIndex is 0-based. TheaterIds null = all theaters.</summary>
public record StaffTaskSearchCriteria(
    IReadOnlyCollection<Guid>? TheaterIds,
    Guid? AssignedToUserId,
    StaffTaskStatus? Status,
    int PageIndex,
    int PageSize);

/// <summary>
/// Rosters, time clock and tasks. Methods that return tracked entities say so: edit them, then call
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

    // ── Time clock ───────────────────────────────────────────────────────────

    /// <summary>The user's open (not clocked out) entry, TRACKED, or null.</summary>
    Task<TimeClockEntry?> GetOpenClockEntryAsync(Guid userId);

    /// <summary>Saves a new entry immediately (the unique open-entry index rejects a second open one).</summary>
    Task AddClockEntryAsync(TimeClockEntry entry);

    /// <summary>Entries of a theater clocked in within [from, to), optionally of one user, newest first, untracked.</summary>
    Task<List<TimeClockEntry>> GetClockEntriesAsync(Guid theaterId, DateTime fromUtc, DateTime toUtc, Guid? userId);

    // ── Tasks ────────────────────────────────────────────────────────────────

    Task<StaffTask?> GetTaskAsync(Guid id);
    void StageTask(StaffTask task);
    Task<(List<StaffTask> Items, int Total)> SearchTasksAsync(StaffTaskSearchCriteria criteria);

    /// <summary>One user's tasks, open/in-progress first then newest; closed ones only when asked. Capped at 200, untracked.</summary>
    Task<List<StaffTask>> GetUserTasksAsync(Guid userId, bool includeClosed);
}
