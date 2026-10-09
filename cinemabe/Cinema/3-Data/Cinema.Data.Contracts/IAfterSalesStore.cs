using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>One invoice found by the after-sales search (read-only projection).</summary>
public sealed class AfterSalesInvoiceRow
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
    public int FoodCount { get; set; }
    public DateTime? FirstShowStart { get; set; }
    public string? MovieTitle { get; set; }
    public Guid? ExchangedFromInvoiceId { get; set; }
}

public sealed class TenderTotalRow
{
    public PaymentTender Method { get; set; }
    public double Amount { get; set; }
    public int Count { get; set; }
}

/// <summary>An invoice refunded in a window. <see cref="ReplacementFinalAmount"/> is set when it was refunded
/// because of an exchange (the final amount of the invoice that replaced it).</summary>
public sealed class RefundedInvoiceRow
{
    public Guid Id { get; set; }
    public double FinalAmount { get; set; }
    public double? ReplacementFinalAmount { get; set; }
}

public sealed class DrawerSessionRow
{
    public Guid Id { get; set; }
    public string TerminalName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public CashDrawerStatus Status { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public double OpeningFloat { get; set; }
    /// <summary>Sum of the session's cash movements right now (live for an Open session).</summary>
    public double MovementTotal { get; set; }
    public double? ExpectedCash { get; set; }
    public double? CountedCash { get; set; }
    public double? Variance { get; set; }
}

public sealed class SalesVolumeRow
{
    public int TicketsSold { get; set; }
    public int FoodItemsSold { get; set; }
    public double FoodRevenue { get; set; }
}

/// <summary>Queries for after-sales (refund/exchange/reprint search) and the daily cash close. All reads are
/// no-tracking projections; money windows are UTC half-open ranges [from, to).</summary>
public interface IAfterSalesStore
{
    /// <summary>Atomically moves a Paid invoice to Refunded (single conditional UPDATE). False when it was not Paid
    /// any more, so two simultaneous refunds cannot both pay out.</summary>
    Task<bool> TryClaimRefundAsync(Guid invoiceId, StaffReasonCode? reason, DateTime nowUtc);

    /// <summary>Invoices of the theater matched by exact code and/or customer phone, newest first.</summary>
    Task<List<AfterSalesInvoiceRow>> FindInvoicesAsync(Guid theaterId, string? code, string? phone, int take);

    /// <summary>Sum and count of InvoicePayment rows per method, for Paid/Refunded invoices paid in the window.</summary>
    Task<List<TenderTotalRow>> GetTenderTotalsAsync(Guid theaterId, DateTime fromUtc, DateTime toUtc);

    Task<List<RefundedInvoiceRow>> GetRefundedInvoicesAsync(Guid theaterId, DateTime fromUtc, DateTime toUtc);

    /// <summary>Tickets and food sold on Paid invoices paid in the window.</summary>
    Task<SalesVolumeRow> GetSalesVolumeAsync(Guid theaterId, DateTime fromUtc, DateTime toUtc);

    /// <summary>Number and total amount of audit rows of an action in the window.</summary>
    Task<(int Count, double Amount)> GetAuditTotalsAsync(Guid theaterId, AuditAction action, DateTime fromUtc, DateTime toUtc);

    /// <summary>Drawer sessions opened in the window, with their live movement total (one query).</summary>
    Task<List<DrawerSessionRow>> GetDrawerSessionsAsync(Guid theaterId, DateTime fromUtc, DateTime toUtc);
}
