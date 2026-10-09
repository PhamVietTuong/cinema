using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.CustomerService;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
using Cinema.Foundation.Logging;
using Cinema.Service.WebApiHost.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Service.WebApiHost.Controllers;

public partial class CinemaController
{
    #region CustomerService

    /// <summary>Sellers plus the regional manager, who may follow up and approve compensation.</summary>
    private const string _complaintRoles = RoleNames.Sellers + "," + RoleNames.RegionalManager;

    /// <summary>Finds a member by email, phone or invoice code: masked contact, tier, points and the last 20 invoices of the caller's theaters.</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(CustomerLookupDTO), 200)]
    public async Task<IActionResult> LookupCustomer([FromBody] LookupCustomerRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(LookupCustomer)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _customerService.LookupCustomerAsync(scope.ToTheaterFilter(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(LookupCustomer));
        }
    }

    /// <summary>Sends the e-ticket of a paid invoice again by email or SMS (audited, 3 per hour per invoice).</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(ResendETicketResultDTO), 200)]
    public async Task<IActionResult> ResendETicket([FromBody] ResendETicketRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(ResendETicket)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _customerService.ResendETicketAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ResendETicket));
        }
    }

    [Authorize(Roles = _complaintRoles)]
    [HttpPost]
    [ProducesResponseType(typeof(ComplaintDTO), 200)]
    public async Task<IActionResult> CreateComplaint([FromBody] CreateComplaintRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(CreateComplaint)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _customerService.CreateComplaintAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(CreateComplaint));
        }
    }

    /// <summary>Complaint page, newest first. Filters: status, category, assignedTo, customerId, invoiceId, theaterId (must be in scope, else 403).</summary>
    [Authorize(Roles = _complaintRoles)]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<ComplaintDTO>), 200)]
    public async Task<IActionResult> GetComplaints([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetComplaints)} being awakened to process request...");
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

            return Ok(await _customerService.SearchComplaintsAsync(theaterIds, search ?? new PagingSearchDTO()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetComplaints));
        }
    }

    [Authorize(Roles = _complaintRoles)]
    [HttpPost]
    [ProducesResponseType(typeof(ComplaintDTO), 200)]
    public async Task<IActionResult> GetComplaint([FromBody] GetComplaintRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetComplaint)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _customerService.GetComplaintAsync(scope.ToTheaterFilter(), request.ComplaintId));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetComplaint));
        }
    }

    [Authorize(Roles = _complaintRoles)]
    [HttpPost]
    [ProducesResponseType(typeof(ComplaintDTO), 200)]
    public async Task<IActionResult> UpdateComplaint([FromBody] UpdateComplaintRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(UpdateComplaint)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _customerService.UpdateComplaintAsync(scope.ToTheaterFilter(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(UpdateComplaint));
        }
    }

    /// <summary>Open to InReview, assigned to a staff member (default: the caller).</summary>
    [Authorize(Roles = _complaintRoles)]
    [HttpPost]
    [ProducesResponseType(typeof(ComplaintDTO), 200)]
    public async Task<IActionResult> StartComplaintReview([FromBody] StartComplaintReviewRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(StartComplaintReview)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _customerService.StartComplaintReviewAsync(scope.ToTheaterFilter(), User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(StartComplaintReview));
        }
    }

    [Authorize(Roles = _complaintRoles)]
    [HttpPost]
    [ProducesResponseType(typeof(ComplaintDTO), 200)]
    public async Task<IActionResult> RejectComplaint([FromBody] RejectComplaintRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(RejectComplaint)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _customerService.RejectComplaintAsync(scope.ToTheaterFilter(), User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(RejectComplaint));
        }
    }

    /// <summary>Resolves a complaint with Refund, GiftCard, Points or Apology. Compensation needs an approver or a manager override (else 403).</summary>
    [Authorize(Roles = _complaintRoles)]
    [HttpPost]
    [ProducesResponseType(typeof(ComplaintDTO), 200)]
    public async Task<IActionResult> ResolveComplaint([FromBody] ResolveComplaintRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(ResolveComplaint)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _customerService.ResolveComplaintAsync(scope.ToTheaterFilter(), User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ResolveComplaint));
        }
    }

    #endregion
}
