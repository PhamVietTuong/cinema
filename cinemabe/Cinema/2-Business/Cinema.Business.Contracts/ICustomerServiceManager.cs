using Cinema.Business.DTO.CustomerService;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

/// <summary>
/// Staff customer service: member lookup, e-ticket resend and the complaint workflow with compensation. The
/// <c>theaterId</c> arguments are already resolved against the caller's scope by the controller; <c>scopeTheaterIds</c>
/// (null = every theater) refuses data of a theater outside the caller's scope. Authorization failures throw
/// <c>AccessDeniedException</c> (403), never <c>UnauthorizedAccessException</c> (401).
/// </summary>
public interface ICustomerServiceManager
{
    /// <summary>
    /// Finds a member by exact email, exact phone or invoice code. Returns name, masked contact, tier, points and the
    /// last 20 invoices of theaters in scope. The member profile itself is visible to any staff.
    /// </summary>
    Task<CustomerLookupDTO> LookupCustomerAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, LookupCustomerRequest request);

    /// <summary>
    /// Sends the e-ticket of a Paid invoice of the theater again by email or SMS. Audited (ResendTicket); at most 3 per
    /// hour per invoice (the 4th is <c>InvalidOperationException</c>, 400).
    /// </summary>
    Task<ResendETicketResultDTO> ResendETicketAsync(Guid theaterId, Guid staffUserId, ResendETicketRequest request);

    Task<ComplaintDTO> CreateComplaintAsync(Guid theaterId, Guid staffUserId, CreateComplaintRequest request);

    /// <summary>Edits category/description while the complaint is Open or InReview.</summary>
    Task<ComplaintDTO> UpdateComplaintAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, UpdateComplaintRequest request);

    Task<ComplaintDTO> GetComplaintAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid complaintId);

    /// <summary>Complaint page, newest first. Filters: status, category, assignedTo, customerId, invoiceId.</summary>
    Task<DefaultSearchResults<ComplaintDTO>> SearchComplaintsAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, PagingSearchDTO search);

    /// <summary>Open to InReview, assigned to the given staff member (default: the caller).</summary>
    Task<ComplaintDTO> StartComplaintReviewAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid staffUserId, StartComplaintReviewRequest request);

    /// <summary>Open or InReview to Rejected with a reason.</summary>
    Task<ComplaintDTO> RejectComplaintAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid staffUserId, RejectComplaintRequest request);

    /// <summary>
    /// Open or InReview to Resolved. Refund delegates to <c>IBoxOfficeManager.StaffRefundAsync</c>, GiftCard issues a card
    /// to the customer's email, Points adds points (audited PointsAdjust), Apology gives nothing. Everything except
    /// Apology needs an approver or a manager override, verified before any transaction opens; every resolution is
    /// audited as <c>AuditAction.Compensation</c> with actor and approver.
    /// </summary>
    Task<ComplaintDTO> ResolveComplaintAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid staffUserId, ResolveComplaintRequest request);
}
