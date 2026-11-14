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
    /// <summary>Loyalty points spent on this booking, reserved at creation and restored if it is
    /// cancelled or expires.</summary>
    public int PointsRedeemed { get; set; }
    /// <summary>Vestigial: the old Discount entity has been merged into PromotionProgram and removed.
    /// Historical rows only — no FK, no navigation property. New invoices never set this; see
    /// PromotionProgramId instead.</summary>
    public Guid? DiscountId { get; set; }
    /// <summary>Set when a PromotionProgram (by code or auto-apply) produced the price for this invoice.</summary>
    public Guid? PromotionProgramId { get; set; }
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
    /// <summary>The invoice this one replaced in a counter exchange.</summary>
    public Guid? ExchangedFromInvoiceId { get; set; }
    public User? User { get; set; }
    public PromotionProgram? PromotionProgram { get; set; }
    public ICollection<InvoiceTicket> InvoiceTickets { get; set; } = new List<InvoiceTicket>();
    public ICollection<InvoiceFoodAndDrink> InvoiceFoodAndDrinks { get; set; } = new List<InvoiceFoodAndDrink>();
    public ICollection<InvoicePayment> Payments { get; set; } = new List<InvoicePayment>();
}
