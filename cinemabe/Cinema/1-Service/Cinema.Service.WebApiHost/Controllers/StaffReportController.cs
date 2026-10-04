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

    // ── P9 reporting (append new actions below) ──────────────────────────────

    /// <summary>
    /// Sales grouped by Day, Movie, Theater, PaymentMethod, Staff or Channel: ticket and F&amp;B revenue separate, net of
    /// refunds. Range of business dates, at most 92 days. A theater outside the caller's scope is refused with 403.
    /// </summary>
    [Authorize(Roles = RoleNames.Reporting)]
    [HttpPost]
    [ProducesResponseType(typeof(SalesReportDTO), 200)]
    public async Task<IActionResult> GetSales([FromBody] StaffReportRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetSales)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _reports.GetSalesAsync(request, scope.ToTheaterFilter()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetSales));
        }
    }

    /// <summary>Sold / active seats per screening starting in the range. Scope and range rules as for sales.</summary>
    [Authorize(Roles = RoleNames.Reporting)]
    [HttpPost]
    [ProducesResponseType(typeof(OccupancyReportDTO), 200)]
    public async Task<IActionResult> GetOccupancy([FromBody] StaffReportRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetOccupancy)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _reports.GetOccupancyAsync(request, scope.ToTheaterFilter()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetOccupancy));
        }
    }

    /// <summary>Attach rate, refund rate (count and amount) and average spend per head. Scope and range rules as for sales.</summary>
    [Authorize(Roles = RoleNames.Reporting)]
    [HttpPost]
    [ProducesResponseType(typeof(StaffKpisDTO), 200)]
    public async Task<IActionResult> GetKpis([FromBody] StaffReportRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetKpis)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _reports.GetKpisAsync(request, scope.ToTheaterFilter()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetKpis));
        }
    }
}
