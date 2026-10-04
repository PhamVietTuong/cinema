using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Staff;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.BoxOffice;

/// <summary>After-sales search. At least one of <see cref="Code"/> or <see cref="Phone"/> is required.</summary>
public class FindInvoiceRequest
{
    public Guid? TheaterId { get; set; }

    /// <summary>Exact invoice code.</summary>
    [StringLength(50)]
    public string? Code { get; set; }

    /// <summary>Exact customer phone number.</summary>
    [StringLength(30)]
    public string? Phone { get; set; }
}

public class AfterSalesInvoiceDTO
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public InvoiceStatus Status { get; set; }
    public SalesChannel Channel { get; set; }
    public double FinalAmount { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public int TicketCount { get; set; }
    public int UsedTicketCount { get; set; }
    public bool HasFood { get; set; }
    /// <summary>Earliest showtime start of the invoice's tickets (theater local time).</summary>
    public DateTime? FirstShowStart { get; set; }
    public string? MovieTitle { get; set; }
    public Guid? ExchangedFromInvoiceId { get; set; }
    /// <summary>The first showtime has already started (a refund/exchange then needs a manager).</summary>
    public bool ShowStarted { get; set; }
    /// <summary>Paid, with no ticket used yet: a refund can be attempted.</summary>
    public bool CanRefund { get; set; }
}

public class StaffRefundRequest
{
    public Guid? TheaterId { get; set; }

    [NotEmptyGuid]
    public Guid InvoiceId { get; set; }

    public StaffReasonCode ReasonCode { get; set; }

    /// <summary>Free-text note. Required when the reason is Other.</summary>
    [StringLength(500)]
    public string? Note { get; set; }

    /// <summary>How the money goes back for a counter invoice: Cash (out of the open drawer), Card or QrWallet.
    /// Ignored for an online invoice (the gateway is used) and for an invoice that cost nothing.</summary>
    public PaymentTender RefundTender { get; set; } = PaymentTender.Cash;

    /// <summary>Terminal / wallet reference of the card or QR refund. Required for those tenders.</summary>
    [StringLength(100)]
    public string? RefundReference { get; set; }

    /// <summary>Manager approval. Not needed when the caller is a manager.</summary>
    public ManagerOverrideDTO? Override { get; set; }
}

public class StaffRefundResultDTO
{
    public Guid InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public double RefundedAmount { get; set; }
    /// <summary>Tender the money went back through (Online for a gateway invoice).</summary>
    public PaymentTender RefundTender { get; set; }
    /// <summary>Cash that left the drawer.</summary>
    public double CashReturned { get; set; }
    public DateTime RefundedAt { get; set; }
    /// <summary>The refund was recorded after the showtime had started (manager approved).</summary>
    public bool AfterShowStart { get; set; }
    /// <summary>An online invoice whose gateway declined the API refund: the approver must return it from the merchant portal.</summary>
    public bool OutOfBand { get; set; }
}

public class ExchangeRequest
{
    public Guid? TheaterId { get; set; }

    /// <summary>The counter invoice being replaced.</summary>
    [NotEmptyGuid]
    public Guid InvoiceId { get; set; }

    /// <summary>The replacement sale. Its tenders settle only the difference over the old invoice's final amount;
    /// when the new sale is cheaper, the difference is paid back through <see cref="RefundTender"/>.</summary>
    [Required]
    public CounterSaleRequest NewSale { get; set; } = new();

    public StaffReasonCode ReasonCode { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    /// <summary>How a price difference in the customer's favour is paid back (Cash or Card/QrWallet).</summary>
    public PaymentTender RefundTender { get; set; } = PaymentTender.Cash;

    public ManagerOverrideDTO? Override { get; set; }
}

public class ExchangeResultDTO
{
    public Guid OldInvoiceId { get; set; }
    public string OldInvoiceCode { get; set; } = string.Empty;
    public CounterSaleResultDTO NewSale { get; set; } = new();
    /// <summary>What the customer paid on top (new final amount minus old, when positive).</summary>
    public double AmountCollected { get; set; }
    /// <summary>What was paid back (old final amount minus new, when positive).</summary>
    public double RefundedBack { get; set; }
}

public class ReprintRequest
{
    public Guid? TheaterId { get; set; }

    [NotEmptyGuid]
    public Guid InvoiceId { get; set; }

    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Needed only when a ticket of the invoice has already been used.</summary>
    public ManagerOverrideDTO? Override { get; set; }
}

public class ReprintResultDTO
{
    public Guid InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public double FinalAmount { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? MovieTitle { get; set; }
    public string? RoomName { get; set; }
    public DateTime? ShowStart { get; set; }
    public List<TicketItemDTO> Tickets { get; set; } = new();
}

public class CloseDrawerRequest
{
    public Guid? TheaterId { get; set; }

    [NotEmptyGuid]
    public Guid SessionId { get; set; }

    /// <summary>The cash physically counted in the drawer.</summary>
    [Range(0, 100000000000)]
    public double CountedCash { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public class CloseDrawerResultDTO
{
    public Guid SessionId { get; set; }
    public CashDrawerStatus Status { get; set; }
    public double ExpectedCash { get; set; }
    public double CountedCash { get; set; }
    /// <summary>Counted minus expected (negative = short).</summary>
    public double Variance { get; set; }
    /// <summary>The variance exceeds the tolerance: the session stays Closed until a manager reconciles it.</summary>
    public bool NeedsReconciliation { get; set; }
}

public class ReconcileDrawerRequest
{
    public Guid? TheaterId { get; set; }

    [NotEmptyGuid]
    public Guid SessionId { get; set; }

    [Required]
    [StringLength(500)]
    public string Note { get; set; } = string.Empty;

    public ManagerOverrideDTO? Override { get; set; }
}

public class DailyCloseRequest
{
    public Guid? TheaterId { get; set; }

    /// <summary>The business day (only the date part is used). Defaults to the current business day.</summary>
    public DateTime? BusinessDate { get; set; }
}

public class DailyCloseTenderDTO
{
    public PaymentTender Method { get; set; }
    public double Amount { get; set; }
    public int Count { get; set; }
}

public class DailyCloseDrawerDTO
{
    public Guid SessionId { get; set; }
    public string TerminalName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public CashDrawerStatus Status { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public double OpeningFloat { get; set; }
    public double ExpectedCash { get; set; }
    public double? CountedCash { get; set; }
    public double? Variance { get; set; }
}

public class DailyCloseDTO
{
    public Guid TheaterId { get; set; }
    /// <summary>The business date requested (date only).</summary>
    public DateTime BusinessDate { get; set; }
    /// <summary>Start of the business day in UTC (inclusive).</summary>
    public DateTime FromUtc { get; set; }
    /// <summary>End of the business day in UTC (exclusive).</summary>
    public DateTime ToUtc { get; set; }
    /// <summary>Sum of InvoicePayment rows per method for invoices paid in the day (Paid and later Refunded).</summary>
    public List<DailyCloseTenderDTO> Tenders { get; set; } = new();
    /// <summary>Sum of every InvoicePayment row (including Points and GiftCard rows, which are inside the discount).</summary>
    public double PaymentsTotal { get; set; }
    /// <summary>Cash + Card + QrWallet + Online: real money received.</summary>
    public double MoneyCollected { get; set; }
    public int RefundCount { get; set; }
    /// <summary>Money paid back in the day: whole refunds plus the price difference returned in exchanges.</summary>
    public double RefundAmount { get; set; }
    public int ExchangeCount { get; set; }
    /// <summary>Compensation (comp) records of the day.</summary>
    public int CompCount { get; set; }
    public double CompAmount { get; set; }
    /// <summary>MoneyCollected minus RefundAmount.</summary>
    public double NetCollected { get; set; }
    public int TicketsSold { get; set; }
    public int FoodItemsSold { get; set; }
    public double FoodRevenue { get; set; }
    /// <summary>Sessions opened in the day. Open ones show their live expected cash.</summary>
    public List<DailyCloseDrawerDTO> Drawers { get; set; } = new();
    /// <summary>Closed sessions whose variance still awaits a manager.</summary>
    public int UnreconciledDrawerCount { get; set; }
    public double TotalVariance { get; set; }
}
