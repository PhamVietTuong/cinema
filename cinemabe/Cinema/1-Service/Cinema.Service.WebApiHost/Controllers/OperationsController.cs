using Cinema.Business.Contracts;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Operations;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
using Cinema.Foundation.Logging;
using Cinema.Service.WebApiHost.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Service.WebApiHost.Controllers;

/// <summary>Staff-app day-to-day operations: schedule board, incidents with seat/room blocking, showtime checklists.</summary>
[ApiController]
[Route("api/[controller]/[action]")]
[ApiExplorerSettings(GroupName = "staff")]
public class OperationsController : ApiControllerBase
{
    private const string _theaterIdFilter = "theaterId";

    private readonly IScheduleBoardManager _board;
    private readonly IIncidentManager _incidents;
    private readonly IChecklistManager _checklists;

    public OperationsController(IScheduleBoardManager board, IIncidentManager incidents, IChecklistManager checklists)
    {
        _board = board;
        _incidents = incidents;
        _checklists = checklists;
    }

    /// <summary>One theater's rooms with the day's showtimes: start, end, bufferEnd, movie, sold, capacity and room status.</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(ScheduleBoardDTO), 200)]
    public async Task<IActionResult> GetScheduleBoard([FromBody] ScheduleBoardRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetScheduleBoard)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _board.GetScheduleBoardAsync(theaterId, request.Date));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetScheduleBoard));
        }
    }

    /// <summary>Reports an incident (every staff role).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(IncidentDTO), 200)]
    public async Task<IActionResult> ReportIncident([FromBody] ReportIncidentRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(ReportIncident)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _incidents.ReportAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ReportIncident));
        }
    }

    /// <summary>Incident page, newest first. Filters: status, category, from, to, theaterId (must be in scope, else 403).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<IncidentDTO>), 200)]
    public async Task<IActionResult> GetIncidents([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetIncidents)} being awakened to process request...");
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

            return Ok(await _incidents.SearchAsync(theaterIds, search ?? new PagingSearchDTO()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetIncidents));
        }
    }

    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(IncidentDTO), 200)]
    public async Task<IActionResult> GetIncident([FromBody] GetIncidentRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetIncident)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _incidents.GetAsync(scope.ToTheaterFilter(), request.IncidentId));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetIncident));
        }
    }

    /// <summary>Closes an incident; with Unblock it also reopens the blocked seat/room (approver or manager override).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(IncidentDTO), 200)]
    public async Task<IActionResult> ResolveIncident([FromBody] ResolveIncidentRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(ResolveIncident)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _incidents.ResolveAsync(scope.ToTheaterFilter(), User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ResolveIncident));
        }
    }

    /// <summary>
    /// Takes a seat (and its double-seat partner) out of sale. Approvers, or any staff with a manager override (403
    /// without one). Returns the upcoming tickets on the seat; nothing is cancelled.
    /// </summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(BlockResultDTO), 200)]
    public async Task<IActionResult> BlockSeat([FromBody] BlockSeatRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(BlockSeat)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _incidents.BlockSeatAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(BlockSeat));
        }
    }

    /// <summary>
    /// Puts a room into maintenance. Approvers, or any staff with a manager override (403 without one). Returns the
    /// upcoming tickets in the room; nothing is cancelled.
    /// </summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(BlockResultDTO), 200)]
    public async Task<IActionResult> BlockRoom([FromBody] BlockRoomRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(BlockRoom)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _incidents.BlockRoomAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(BlockRoom));
        }
    }

    /// <summary>The theater's checklist templates (approvers manage them).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(List<ChecklistTemplateDTO>), 200)]
    public async Task<IActionResult> GetChecklistTemplates([FromBody] GetChecklistTemplatesRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetChecklistTemplates)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _checklists.GetTemplatesAsync(theaterId));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetChecklistTemplates));
        }
    }

    /// <summary>Creates or edits a checklist template (approvers only; one active template per theater and kind).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(ChecklistTemplateDTO), 200)]
    public async Task<IActionResult> SaveChecklistTemplate([FromBody] SaveChecklistTemplateRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(SaveChecklistTemplate)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _checklists.SaveTemplateAsync(scope.ToTheaterFilter(), theaterId, request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(SaveChecklistTemplate));
        }
    }

    /// <summary>Opens a showtime's checklist, creating the run from the active template the first time (every staff role).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(ChecklistRunDTO), 200)]
    public async Task<IActionResult> OpenChecklist([FromBody] OpenChecklistRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(OpenChecklist)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _checklists.OpenAsync(theaterId, request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(OpenChecklist));
        }
    }

    /// <summary>Ticks or unticks one checklist item (every staff role).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(ChecklistRunDTO), 200)]
    public async Task<IActionResult> SetChecklistItem([FromBody] SetChecklistItemRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(SetChecklistItem)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _checklists.SetItemAsync(scope.ToTheaterFilter(), User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(SetChecklistItem));
        }
    }

    /// <summary>Completes a checklist; every required item must be done (400 otherwise).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(ChecklistRunDTO), 200)]
    public async Task<IActionResult> CompleteChecklist([FromBody] CompleteChecklistRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(CompleteChecklist)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _checklists.CompleteAsync(scope.ToTheaterFilter(), User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(CompleteChecklist));
        }
    }
}
