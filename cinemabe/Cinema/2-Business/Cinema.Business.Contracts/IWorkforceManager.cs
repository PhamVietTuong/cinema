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
}
