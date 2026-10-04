using Cinema.Business.DTO.Requests;
using Cinema.Business.DTO.Staff;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

/// <summary>
/// Rosters, the time clock and staff tasks. <c>theaterId</c> arguments are already resolved against the caller's
/// scope by the controller; <c>scopeTheaterIds</c> (null = every theater) refuses records of a theater outside the
/// scope (<c>AccessDeniedException</c>, 403). Roster/template/task management is for approvers (enforced on the
/// controller). Decision D8: clocking in is NOT required to sell anything; the time clock is reporting only.
/// </summary>
public interface IWorkforceManager
{
    Task<List<TheaterStaffDTO>> GetTheaterStaffAsync(Guid theaterId);

    Task<List<StaffShiftDTO>> GetRosterAsync(Guid theaterId, RosterRequest request);
    Task<List<StaffShiftDTO>> GetMyShiftsAsync(Guid userId, MyShiftsRequest request);

    /// <summary>Creates/edits a shift. The user must be an active staff member of the theater with no overlapping shift (400 otherwise).</summary>
    Task<StaffShiftDTO> SaveShiftAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid theaterId, SaveStaffShiftRequest request);
    Task DeleteShiftAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, DeleteStaffShiftRequest request);

    /// <summary>Starts a time-clock entry. A second clock-in while one is open is 400 (also enforced by a unique index).</summary>
    Task<TimeClockEntryDTO> ClockInAsync(Guid theaterId, Guid userId, ClockInRequest request);

    /// <summary>Closes the caller's open entry; 400 when they are not clocked in.</summary>
    Task<TimeClockEntryDTO> ClockOutAsync(Guid userId, ClockOutRequest request);
    Task<ClockStatusDTO> GetMyClockStatusAsync(Guid userId);

    /// <summary>Clock entries of a theater in a range (at most 31 days), with worked minutes. Reporting only (D8).</summary>
    Task<List<TimeClockEntryDTO>> GetTimeSheetAsync(Guid theaterId, TimeSheetRequest request);

    /// <summary>Creates/edits a task assigned to a staff member of the theater, optionally linked to an incident or checklist run.</summary>
    Task<StaffTaskDTO> SaveTaskAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid theaterId, Guid actorUserId, SaveStaffTaskRequest request);

    /// <summary>Task page, newest first. Filters: assignedTo, status.</summary>
    Task<DefaultSearchResults<StaffTaskDTO>> GetTasksAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, PagingSearchDTO search);
    Task<List<StaffTaskDTO>> GetMyTasksAsync(Guid userId, MyTasksRequest request);

    /// <summary>Moves one of the caller's own tasks between Open, InProgress and Done (403 for someone else's task).</summary>
    Task<StaffTaskDTO> SetMyTaskStatusAsync(Guid userId, SetMyTaskStatusRequest request);
}
