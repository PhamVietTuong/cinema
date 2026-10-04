using Cinema.Business.Contracts;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.BoxOffice;
using Cinema.Foundation.Logging;
using Cinema.Service.WebApiHost.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Service.WebApiHost.Controllers;

/// <summary>Staff point of sale: counter ticket and food sales with split tenders, and the cashier's cash drawer.</summary>
[ApiController]
[Route("api/[controller]/[action]")]
[ApiExplorerSettings(GroupName = "staff")]
[Authorize(Roles = RoleNames.Sellers)]
public class BoxOfficeController : ApiControllerBase
{
    private readonly IBoxOfficeManager _boxOffice;

    public BoxOfficeController(IBoxOfficeManager boxOffice)
    {
        _boxOffice = boxOffice;
    }

    /// <summary>Prices a counter sale (seats and/or food) without changing anything.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CounterQuoteDTO), 200)]
    public async Task<IActionResult> Quote([FromBody] CounterSaleRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(Quote)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.QuoteAsync(theaterId, request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(Quote));
        }
    }

    /// <summary>Rings up a counter sale: a Paid invoice with its tenders, never a Pending one.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CounterSaleResultDTO), 200)]
    public async Task<IActionResult> Sell([FromBody] CounterSaleRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(Sell)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.SellAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(Sell));
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(CashDrawerDTO), 200)]
    public async Task<IActionResult> OpenDrawer([FromBody] OpenDrawerRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(OpenDrawer)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.OpenDrawerAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(OpenDrawer));
        }
    }

    /// <summary>The caller's open drawer with totals; <c>IsOpen = false</c> when there is none.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CashDrawerDTO), 200)]
    public async Task<IActionResult> GetMyDrawer([FromBody] BoxOfficeScopeRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetMyDrawer)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.GetMyDrawerAsync(theaterId, User.GetUserId()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetMyDrawer));
        }
    }

    /// <summary>Records cash put into or taken out of the drawer. A pay-out needs a manager override.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CashDrawerDTO), 200)]
    public async Task<IActionResult> PayInOut([FromBody] PayInOutRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(PayInOut)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.PayInOutAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(PayInOut));
        }
    }

    /// <summary>The theater's showtimes of a business day (default today), one row per showtime and room.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(List<CounterShowtimeDTO>), 200)]
    public async Task<IActionResult> GetShowtimesToday([FromBody] ShowtimesTodayRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetShowtimesToday)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.GetShowtimesTodayAsync(theaterId, request.Date));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetShowtimesToday));
        }
    }

    /// <summary>Finds a member by exact phone number to attach to a sale (404 when none).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CounterCustomerDTO), 200)]
    public async Task<IActionResult> FindCustomer([FromBody] FindCustomerRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(FindCustomer)} being awakened to process request...");
        try
        {
            return Ok(await _boxOffice.FindCustomerAsync(request.Phone));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(FindCustomer));
        }
    }
}
