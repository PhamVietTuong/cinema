using Cinema.Data.Enums;

namespace Cinema.Data.Entities;

/// <summary>
/// One tender applied to an invoice. <see cref="Amount"/> is what the tender contributed to the invoice (for
/// cash: the amount due, never the amount handed over). For cash rows <see cref="TenderedAmount"/> is what the
/// customer handed over and <see cref="ChangeAmount"/> what was given back. Points and GiftCard rows record the
/// part of the price covered by redeemed points / a gift card (already inside Invoice.DiscountAmount).
/// </summary>
public class InvoicePayment : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public PaymentTender Method { get; set; }
    public double Amount { get; set; }
    public double? TenderedAmount { get; set; }
    public double? ChangeAmount { get; set; }
    /// <summary>Card/QR terminal reference, gift-card code or gateway reference.</summary>
    public string? Reference { get; set; }
    public Invoice Invoice { get; set; } = null!;
}
