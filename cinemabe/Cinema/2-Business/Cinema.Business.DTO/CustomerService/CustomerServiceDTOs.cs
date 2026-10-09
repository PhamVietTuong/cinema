using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Staff;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.CustomerService;

// ── Customer lookup ──────────────────────────────────────────────────────────

public class LookupCustomerRequest
{
    /// <summary>An exact email, an exact phone number or an invoice code.</summary>
    [Required]
    [StringLength(200)]
    public string Query { get; set; } = string.Empty;
}

/// <summary>A member profile for the customer card. Contact details are masked.</summary>
public class CustomerCardDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? MaskedEmail { get; set; }
    public string? MaskedPhone { get; set; }
    public string? MembershipName { get; set; }
    public int Points { get; set; }
}

public class CustomerInvoiceDTO
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid? TheaterId { get; set; }
    public string? TheaterName { get; set; }
    public InvoiceStatus Status { get; set; }
    public SalesChannel Channel { get; set; }
    public double FinalAmount { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreationTime { get; set; }
    public string? MovieTitle { get; set; }
    public DateTime? FirstShowStart { get; set; }
    public int TicketCount { get; set; }
}

public class CustomerLookupDTO
{
    /// <summary>False when nothing matched (no member, and no invoice in the caller's scope for a code).</summary>
    public bool Found { get; set; }

    /// <summary>The member profile; null for a walk-in invoice or no match.</summary>
    public CustomerCardDTO? Customer { get; set; }

    /// <summary>Up to 20 newest invoices, only of theaters in the caller's scope.</summary>
    public List<CustomerInvoiceDTO> Invoices { get; set; } = new();
}

// ── E-ticket resend ──────────────────────────────────────────────────────────

public enum ETicketChannel
{
    Email = 0,
    Sms = 1
}

public class ResendETicketRequest
{
    public Guid? TheaterId { get; set; }

    [NotEmptyGuid]
    public Guid InvoiceId { get; set; }

    public ETicketChannel Channel { get; set; }

    /// <summary>The email or phone to send to. Needed only when the invoice has no customer contact (walk-in sale).</summary>
    [StringLength(200)]
    public string? Address { get; set; }
}

public class ResendETicketResultDTO
{
    public Guid InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public ETicketChannel Channel { get; set; }
    public string MaskedAddress { get; set; } = string.Empty;
    /// <summary>Resends left for this invoice within the current hour.</summary>
    public int RemainingThisHour { get; set; }
}

// ── Complaints ───────────────────────────────────────────────────────────────

public class ComplaintDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public Guid? CustomerUserId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceCode { get; set; }
    public ComplaintCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public ComplaintStatus Status { get; set; }
    public ComplaintResolution Resolution { get; set; }
    public double? CompensationAmount { get; set; }
    public string? CompensationRef { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreateComplaintRequest
{
    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }

    public ComplaintCategory Category { get; set; }

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Optional member the complaint is about. Defaults to the owner of <see cref="InvoiceId"/>.</summary>
    public Guid? CustomerUserId { get; set; }

    /// <summary>Optional invoice of the same theater the complaint is about.</summary>
    public Guid? InvoiceId { get; set; }
}

public class UpdateComplaintRequest
{
    [NotEmptyGuid]
    public Guid ComplaintId { get; set; }

    public ComplaintCategory Category { get; set; }

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;
}

public class GetComplaintRequest
{
    [NotEmptyGuid]
    public Guid ComplaintId { get; set; }
}

/// <summary>Moves an Open complaint to InReview and assigns it.</summary>
public class StartComplaintReviewRequest
{
    [NotEmptyGuid]
    public Guid ComplaintId { get; set; }

    /// <summary>The staff member who takes it. Defaults to the caller.</summary>
    public Guid? AssignToUserId { get; set; }
}

public class RejectComplaintRequest
{
    [NotEmptyGuid]
    public Guid ComplaintId { get; set; }

    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public class ResolveComplaintRequest
{
    [NotEmptyGuid]
    public Guid ComplaintId { get; set; }

    /// <summary>Refund, GiftCard, Points or Apology (None is invalid).</summary>
    public ComplaintResolution Resolution { get; set; }

    /// <summary>Gift card value (VND) or whole points. Ignored for Refund (the whole invoice goes back) and Apology.</summary>
    public double? Amount { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    /// <summary>Refund only, counter invoices: Cash, Card or QrWallet.</summary>
    public PaymentTender RefundTender { get; set; } = PaymentTender.Cash;

    /// <summary>Refund only: terminal/wallet reference of a Card or QrWallet refund.</summary>
    [StringLength(100)]
    public string? RefundReference { get; set; }

    /// <summary>Manager approval. Not needed when the caller is an approver; never needed for an Apology.</summary>
    public ManagerOverrideDTO? Override { get; set; }
}
