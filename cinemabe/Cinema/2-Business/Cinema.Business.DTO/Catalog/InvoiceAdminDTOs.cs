using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

public class InvoiceAdminDTO
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public double TotalAmount { get; set; }
    public double DiscountAmount { get; set; }
    public double FinalAmount { get; set; }
    public InvoiceStatus Status { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreationTime { get; set; }
}

public class UpdateInvoiceStatusRequest
{
    public Guid Id { get; set; }
    public InvoiceStatus Status { get; set; }
}
