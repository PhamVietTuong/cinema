using Cinema.Business.Contracts;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Requests;
using Cinema.Business.DTO.Staff;
using Cinema.Data.Entities;
using Cinema.Foundation.Logging;
using Cinema.Service.WebApiHost.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Service.WebApiHost.Controllers;

/// <summary>Staff-app endpoints about the workforce itself: override PIN and who may approve an override.</summary>
[ApiController]
[Route("api/[controller]/[action]")]
[ApiExplorerSettings(GroupName = "staff")]
public class WorkforceController : ApiControllerBase
{
    private const string _theaterIdFilter = "theaterId";

    private readonly IManagerOverrideService _overrides;
    private readonly IWorkforceManager _workforce;

    public WorkforceController(IManagerOverrideService overrides, IWorkforceManager workforce)
    {
        _overrides = overrides;
        _workforce = workforce;
    }

    /// <summary>Sets the caller's own manager-override PIN (approver roles only).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(204)]
    public async Task<IActionResult> SetMyOverridePin([FromBody] SetOverridePinRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(SetMyOverridePin)} being awakened to process request...");
        try
        {
            await _overrides.SetPinAsync(User.GetUserId(), request.Pin);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(SetMyOverridePin));
        }
    }

    /// <summary>Active approvers (Id, Name) who can authorise an override in the theater.</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(List<OverrideApproverDTO>), 200)]
    public async Task<IActionResult> GetOverrideApprovers([FromBody] OverrideApproversRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetOverrideApprovers)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _overrides.GetApproversAsync(theaterId));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetOverrideApprovers));
        }
    }

    // ── Roster (approvers manage it; every staff member reads their own shifts) ─

    /// <summary>Active staff of the theater, for the roster and task pickers (approvers only).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(List<TheaterStaffDTO>), 200)]
    public async Task<IActionResult> GetTheaterStaff([FromBody] GetTheaterStaffRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetTheaterStaff)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _workforce.GetTheaterStaffAsync(theaterId));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetTheaterStaff));
        }
    }

    /// <summary>Shifts of a theater in a range of at most 31 days (approvers only).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(List<StaffShiftDTO>), 200)]
    public async Task<IActionResult> GetRoster([FromBody] RosterRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetRoster)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _workforce.GetRosterAsync(theaterId, request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetRoster));
        }
    }

    /// <summary>Creates or edits a roster shift (approvers only: 403 for every other staff role).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(StaffShiftDTO), 200)]
    public async Task<IActionResult> SaveShift([FromBody] SaveStaffShiftRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(SaveShift)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _workforce.SaveShiftAsync(scope.ToTheaterFilter(), theaterId, request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(SaveShift));
        }
    }

    /// <summary>Deletes a roster shift (approvers only).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteShift([FromBody] DeleteStaffShiftRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(DeleteShift)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            await _workforce.DeleteShiftAsync(scope.ToTheaterFilter(), request);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(DeleteShift));
        }
    }

    /// <summary>The caller's own shifts in a range of at most 31 days (every staff role).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(List<StaffShiftDTO>), 200)]
    public async Task<IActionResult> GetMyShifts([FromBody] MyShiftsRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetMyShifts)} being awakened to process request...");
        try
        {
            return Ok(await _workforce.GetMyShiftsAsync(User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetMyShifts));
        }
    }

    // ── Time clock (every staff role; reporting only, never required to sell) ──

    /// <summary>Clocks the caller in. 400 when they are already clocked in.</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(TimeClockEntryDTO), 200)]
    public async Task<IActionResult> ClockIn([FromBody] ClockInRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(ClockIn)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _workforce.ClockInAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ClockIn));
        }
    }

    /// <summary>Clocks the caller out. 400 when they are not clocked in.</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(TimeClockEntryDTO), 200)]
    public async Task<IActionResult> ClockOut([FromBody] ClockOutRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(ClockOut)} being awakened to process request...");
        try
        {
            return Ok(await _workforce.ClockOutAsync(User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ClockOut));
        }
    }

    /// <summary>Whether the caller is clocked in (every staff role).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(ClockStatusDTO), 200)]
    public async Task<IActionResult> GetMyClockStatus()
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetMyClockStatus)} being awakened to process request...");
        try
        {
            return Ok(await _workforce.GetMyClockStatusAsync(User.GetUserId()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetMyClockStatus));
        }
    }

    /// <summary>Clock entries of a theater with worked minutes, range of at most 31 days (approvers only).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(List<TimeClockEntryDTO>), 200)]
    public async Task<IActionResult> GetTimeSheet([FromBody] TimeSheetRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetTimeSheet)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _workforce.GetTimeSheetAsync(theaterId, request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetTimeSheet));
        }
    }

    // ── Tasks (approvers assign; every staff member works their own) ───────────

    /// <summary>Creates or edits a task assigned to a staff member, optionally linked to an incident or checklist run (approvers only).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(StaffTaskDTO), 200)]
    public async Task<IActionResult> SaveTask([FromBody] SaveStaffTaskRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(SaveTask)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _workforce.SaveTaskAsync(scope.ToTheaterFilter(), theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(SaveTask));
        }
    }

    /// <summary>Task page, newest first. Filters: assignedTo, status, theaterId (must be in scope, else 403). Approvers only.</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<StaffTaskDTO>), 200)]
    public async Task<IActionResult> GetTasks([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetTasks)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }

            IReadOnlyCollection<Guid>? theaterIds = scope.ToTheaterFilter();
            if (search?.Filters != null
                && search.Filters.TryGetValue(_theaterIdFilter, out var requested)
                && Guid.TryParse(requested, out var requestedTheaterId))
            {
                theaterIds = new[] { scope.Resolve(requestedTheaterId) };
            }

            return Ok(await _workforce.GetTasksAsync(theaterIds, search ?? new PagingSearchDTO()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetTasks));
        }
    }

    /// <summary>The caller's own tasks (every staff role).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(List<StaffTaskDTO>), 200)]
    public async Task<IActionResult> GetMyTasks([FromBody] MyTasksRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetMyTasks)} being awakened to process request...");
        try
        {
            return Ok(await _workforce.GetMyTasksAsync(User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetMyTasks));
        }
    }

    /// <summary>Moves one of the caller's own tasks between Open, InProgress and Done (403 for someone else's task).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(StaffTaskDTO), 200)]
    public async Task<IActionResult> SetMyTaskStatus([FromBody] SetMyTaskStatusRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(SetMyTaskStatus)} being awakened to process request...");
        try
        {
            return Ok(await _workforce.SetMyTaskStatusAsync(User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(SetMyTaskStatus));
        }
    }
}
