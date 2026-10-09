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

    /// <summary>After-sales search by exact invoice code and/or customer phone, scoped to the theater.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(List<AfterSalesInvoiceDTO>), 200)]
    public async Task<IActionResult> FindInvoice([FromBody] FindInvoiceRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(FindInvoice)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.FindInvoiceAsync(theaterId, request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(FindInvoice));
        }
    }

    /// <summary>Refunds a whole paid invoice. 403 without a manager approval (unless the caller is a manager).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(StaffRefundResultDTO), 200)]
    public async Task<IActionResult> StaffRefund([FromBody] StaffRefundRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(StaffRefund)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.StaffRefundAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(StaffRefund));
        }
    }

    /// <summary>Replaces a counter invoice by a new sale in one transaction. 403 without a manager approval (unless the caller is a manager).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ExchangeResultDTO), 200)]
    public async Task<IActionResult> Exchange([FromBody] ExchangeRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(Exchange)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.ExchangeAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(Exchange));
        }
    }

    /// <summary>Returns the tickets of a paid invoice again (audited; a manager approves when a ticket was used).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReprintResultDTO), 200)]
    public async Task<IActionResult> Reprint([FromBody] ReprintRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(Reprint)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.ReprintAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(Reprint));
        }
    }

    /// <summary>Closes the caller's own drawer with the counted cash; a variance beyond the tolerance needs reconciliation.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CloseDrawerResultDTO), 200)]
    public async Task<IActionResult> CloseDrawer([FromBody] CloseDrawerRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(CloseDrawer)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.CloseDrawerAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(CloseDrawer));
        }
    }

    /// <summary>A manager accepts the variance of a closed drawer.</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(CloseDrawerResultDTO), 200)]
    public async Task<IActionResult> ReconcileDrawer([FromBody] ReconcileDrawerRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(ReconcileDrawer)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.ReconcileDrawerAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ReconcileDrawer));
        }
    }
}
