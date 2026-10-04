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

/// <summary>Scoped staff-facing reports.</summary>
[ApiController]
[Route("api/[controller]/[action]")]
[ApiExplorerSettings(GroupName = "staff")]
public class StaffReportController : ApiControllerBase
{
    private const string _theaterIdFilter = "theaterId";

    private readonly IStaffReportManager _reports;

    public StaffReportController(IStaffReportManager reports)
    {
        _reports = reports;
    }

    /// <summary>
    /// Audit trail, newest first. Filters: action, from, to, actorId, and optionally theaterId (must be inside the
    /// caller's scope, else 403). Admins see every theater; managers only their own.
    /// </summary>
    [Authorize(Roles = RoleNames.Reporting)]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<AuditLogDTO>), 200)]
    public async Task<IActionResult> GetAuditLog([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetAuditLog)} being awakened to process request...");
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

            return Ok(await _reports.GetAuditLogAsync(search ?? new PagingSearchDTO(), theaterIds));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetAuditLog));
        }
    }
}
