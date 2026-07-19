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
    public Guid? DiscountId { get; set; }
    public User User { get; set; } = null!;
    public Discount? Discount { get; set; }
    public ICollection<InvoiceTicket> InvoiceTickets { get; set; } = new List<InvoiceTicket>();
    public ICollection<InvoiceFoodAndDrink> InvoiceFoodAndDrinks { get; set; } = new List<InvoiceFoodAndDrink>();
}
