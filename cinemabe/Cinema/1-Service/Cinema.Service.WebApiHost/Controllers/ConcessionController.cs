using Cinema.Business.Contracts;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Concession;
using Cinema.Foundation.Logging;
using Cinema.Service.WebApiHost.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Service.WebApiHost.Controllers;

/// <summary>Staff-app concessions: the food pickup queue, order hand-over and low-stock alerts.</summary>
[ApiController]
[Route("api/[controller]/[action]")]
[ApiExplorerSettings(GroupName = "staff")]
[Authorize(Roles = RoleNames.Concession)]
public class ConcessionController : ApiControllerBase
{
    private readonly IConcessionManager _concessions;

    public ConcessionController(IConcessionManager concessions)
    {
        _concessions = concessions;
    }

    /// <summary>Paid orders waiting to be prepared or handed over, by earliest showtime. Day null = today.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(List<PickupOrderDTO>), 200)]
    public async Task<IActionResult> GetPickupQueue([FromBody] GetPickupQueueRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetPickupQueue)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _concessions.GetPickupQueueAsync(theaterId, request.Day));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetPickupQueue));
        }
    }

    /// <summary>Moves an order one step (Preparing, Ready, HandedOver). An illegal move is 400.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PickupOrderDTO), 200)]
    public async Task<IActionResult> SetFoodStatus([FromBody] SetFoodStatusRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(SetFoodStatus)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _concessions.SetFoodStatusAsync(theaterId, User.GetUserId(), request.InvoiceId, request.Status));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(SetFoodStatus));
        }
    }

    /// <summary>Tracked items at or under their low-stock threshold.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(List<LowStockItemDTO>), 200)]
    public async Task<IActionResult> GetLowStock([FromBody] GetLowStockRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetLowStock)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _concessions.GetLowStockAsync(theaterId));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetLowStock));
        }
    }

    /// <summary>Finds an order by the invoice code the customer shows (404 when unknown or another theater's).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PickupOrderDTO), 200)]
    public async Task<IActionResult> LookupPickup([FromBody] LookupPickupRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(LookupPickup)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _concessions.LookupPickupAsync(theaterId, request.Code));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(LookupPickup));
        }
    }
}
