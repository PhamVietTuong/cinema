using Cinema.Business.Contracts;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Staff;
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
    private readonly IManagerOverrideService _overrides;

    public WorkforceController(IManagerOverrideService overrides)
    {
        _overrides = overrides;
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
}
