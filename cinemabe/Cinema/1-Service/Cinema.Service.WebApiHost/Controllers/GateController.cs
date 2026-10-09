using Cinema.Business.Contracts;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Gate;
using Cinema.Foundation.Logging;
using Cinema.Service.WebApiHost.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Service.WebApiHost.Controllers;

/// <summary>Staff-app gate (theater entrance) ticket validation.</summary>
[ApiController]
[Route("api/[controller]/[action]")]
[ApiExplorerSettings(GroupName = "staff")]
public class GateController : ApiControllerBase
{
    private readonly IGateManager _gate;

    public GateController(IGateManager gate)
    {
        _gate = gate;
    }

    /// <summary>
    /// Scans a ticket QR code. A refused scan is still HTTP 200 with the reason in <c>Outcome</c>, so the client's
    /// error interceptor stays quiet; only authorization problems are 403.
    /// </summary>
    [Authorize(Roles = RoleNames.GateKeepers)]
    [HttpPost]
    [ProducesResponseType(typeof(ScanTicketResultDTO), 200)]
    public async Task<IActionResult> Scan([FromBody] ScanTicketRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(Scan)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }

            return Ok(await _gate.ScanAsync(scope.Resolve(request.TheaterId), User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(Scan));
        }
    }

    /// <summary>Finds today's paid tickets of the theater by invoice code or phone (at least one required).</summary>
    [Authorize(Roles = RoleNames.GateKeepers)]
    [HttpPost]
    [ProducesResponseType(typeof(List<GateLookupResultDTO>), 200)]
    public async Task<IActionResult> Lookup([FromBody] GateLookupRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(Lookup)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }

            return Ok(await _gate.LookupAsync(scope.Resolve(request.TheaterId), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(Lookup));
        }
    }
}
