using Cinema.Business.Contracts;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Invoices;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
using Cinema.Foundation.Logging;
using Cinema.Service.WebApiHost.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Service.WebApiHost.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
[Authorize]
[ApiExplorerSettings(GroupName = "payment")]
public class PaymentController : ControllerBase
{
    private const string _adminRole = "Admin";

    private readonly IBookingManager _bookingManager;
    private readonly IInvoiceManager _invoiceManager;

    public PaymentController(IBookingManager bookingManager, IInvoiceManager invoiceManager)
    {
        _bookingManager = bookingManager;
        _invoiceManager = invoiceManager;
    }

    // ── Booking ───────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<SeatDTO>), 200)]
    public async Task<IActionResult> GetSeats([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetSeats being awakened to process request...");
        try
        {
            var result = await _bookingManager.GetSeatsAsync(search);
            return Ok(result);
        }
        catch (Exception e)
        {
            LogProvider.Current.Fatal(e, $"{GetType().Name}.GetSeats->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(BookingResultDTO), 200)]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.CreateBooking being awakened to process request...");
        try
        {
            var result = await _bookingManager.CreateBookingAsync(User.GetUserId(), request);
            return Ok(result);
        }
        catch (Exception e)
        {
            LogProvider.Current.Fatal(e, $"{GetType().Name}.CreateBooking->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    [HttpPost]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.ConfirmPayment being awakened to process request...");
        try
        {
            var success = await _bookingManager.ConfirmPaymentAsync(User.GetUserId(), request.InvoiceId, request.PaymentReference);
            return success
                ? Ok(new { message = "Payment confirmed." })
                : BadRequest(new { error = "Failed to confirm payment." });
        }
        catch (Exception e)
        {
            LogProvider.Current.Fatal(e, $"{GetType().Name}.ConfirmPayment->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    [HttpPost]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CancelBooking([FromBody] CancelBookingRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.CancelBooking being awakened to process request...");
        try
        {
            var success = await _bookingManager.CancelBookingAsync(User.GetUserId(), request.InvoiceId);
            return success
                ? Ok(new { message = "Booking cancelled." })
                : BadRequest(new { error = "Cannot cancel this booking." });
        }
        catch (Exception e)
        {
            LogProvider.Current.Fatal(e, $"{GetType().Name}.CancelBooking->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    // ── Invoices ──────────────────────────────────────────────────────────────

    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<InvoiceDTO>), 200)]
    public async Task<IActionResult> GetMyInvoices([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetMyInvoices being awakened to process request...");
        try
        {
            var result = await _invoiceManager.GetMyInvoicesAsync(User.GetUserId(), search);
            return Ok(result);
        }
        catch (Exception e)
        {
            LogProvider.Current.Fatal(e, $"{GetType().Name}.GetMyInvoices->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(InvoiceDTO), 200)]
    public async Task<IActionResult> GetInvoice([FromQuery] Guid id)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetInvoice being awakened to process request...");
        try
        {
            var result = await _invoiceManager.GetByIdAsync(id, User.GetUserId(), User.IsInRole(_adminRole));
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception e)
        {
            LogProvider.Current.Fatal(e, $"{GetType().Name}.GetInvoice->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<InvoiceDTO>), 200)]
    public async Task<IActionResult> GetInvoices([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetInvoices being awakened to process request...");
        try
        {
            var result = await _invoiceManager.GetInvoicesAsync(search);
            return Ok(result);
        }
        catch (Exception e)
        {
            LogProvider.Current.Fatal(e, $"{GetType().Name}.GetInvoices->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    [Authorize(Roles = _adminRole)]
    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetRevenue([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetRevenue being awakened to process request...");
        try
        {
            var revenue = await _invoiceManager.GetTotalRevenueAsync(from, to);
            return Ok(new { revenue });
        }
        catch (Exception e)
        {
            LogProvider.Current.Fatal(e, $"{GetType().Name}.GetRevenue->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    [Authorize(Roles = _adminRole)]
    [HttpGet]
    [ProducesResponseType(typeof(List<RevenueByDayDTO>), 200)]
    public async Task<IActionResult> GetRevenueByDay([FromQuery] int days = 7)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetRevenueByDay being awakened to process request...");
        try
        {
            var span = days > 0 ? days : 7;
            var to   = DateTime.Now;
            var from = to.Date.AddDays(-(span - 1));
            var result = await _invoiceManager.GetRevenueByDayAsync(from, to);
            return Ok(result);
        }
        catch (Exception e)
        {
            LogProvider.Current.Fatal(e, $"{GetType().Name}.GetRevenueByDay->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }
}

// ── Request classes ───────────────────────────────────────────────────────────

public record ConfirmPaymentRequest(Guid InvoiceId, string PaymentReference);
public record CancelBookingRequest(Guid InvoiceId);
