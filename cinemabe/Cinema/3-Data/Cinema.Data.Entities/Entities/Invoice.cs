using Cinema.Data.Enums;
namespace Cinema.Data.Entities;
public class Invoice : BaseEntity
{
    public new Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public double TotalAmount { get; set; }
    public double DiscountAmount { get; set; } = 0;
    public double FinalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
    public string? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    /// <summary>Loyalty points spent on this booking, reserved at creation and restored if it is
    /// cancelled, expired, or refunded.</summary>
    public int PointsRedeemed { get; set; }
    /// <summary>Gift card applied to this booking (if any) and the amount drawn from it; the amount is
    /// restored to the card if the booking is cancelled, expired, or refunded.</summary>
    public Guid? GiftCardId { get; set; }
    public double GiftCardAmount { get; set; }
    public Guid? DiscountId { get; set; }
    public User User { get; set; } = null!;
    public Discount? Discount { get; set; }
    public ICollection<InvoiceTicket> InvoiceTickets { get; set; } = new List<InvoiceTicket>();
    public ICollection<InvoiceFoodAndDrink> InvoiceFoodAndDrinks { get; set; } = new List<InvoiceFoodAndDrink>();
}
