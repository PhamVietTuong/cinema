using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public sealed class TenderSettlementResult
{
    /// <summary>One cash row (if any cash was handed over) plus one row per Card/QR tender.</summary>
    public List<InvoicePayment> Payments { get; init; } = new();

    /// <summary>The part of the price actually paid in cash (what enters the drawer).</summary>
    public double CashApplied { get; init; }

    /// <summary>Cash handed back to the customer.</summary>
    public double ChangeDue { get; init; }
}
