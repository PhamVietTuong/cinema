using Cinema.Data.Enums;
namespace Cinema.Data.Entities;
public class Invoice : BaseEntity
{
    public new Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    /// <summary>The customer account. Null for a walk-in counter sale (see SoldByUserId).</summary>
    public Guid? UserId { get; set; }
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
    /// <summary>Theater the sale belongs to (null only on rows predating the column).</summary>
    public Guid? TheaterId { get; set; }
    public SalesChannel Channel { get; set; } = SalesChannel.Online;
    /// <summary>Staff member who rang up a counter sale.</summary>
    public Guid? SoldByUserId { get; set; }
    public Guid? CashDrawerSessionId { get; set; }
    /// <summary>Pickup state of the food lines. None when the invoice has no food.</summary>
    public FoodOrderStatus FoodStatus { get; set; } = FoodOrderStatus.None;
    public DateTime? FoodHandedOverAt { get; set; }
    public Guid? FoodHandedOverByUserId { get; set; }
    public User? User { get; set; }
    public Discount? Discount { get; set; }
    public ICollection<InvoiceTicket> InvoiceTickets { get; set; } = new List<InvoiceTicket>();
    public ICollection<InvoiceFoodAndDrink> InvoiceFoodAndDrinks { get; set; } = new List<InvoiceFoodAndDrink>();
    public ICollection<InvoicePayment> Payments { get; set; } = new List<InvoicePayment>();
}
