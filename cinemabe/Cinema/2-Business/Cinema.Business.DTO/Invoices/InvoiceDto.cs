using Cinema.Data.Enums;
namespace Cinema.Business.DTO.Invoices;
public class InvoiceDTO
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public InvoiceStatus Status { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreationTime { get; set; }
    public List<InvoiceTicketDTO> Tickets { get; set; } = new();
    public List<InvoiceFoodDTO> Foods { get; set; } = new();
}

public class InvoiceTicketDTO
{
    public string MovieTitle { get; set; } = string.Empty;
    public string TheaterName { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DateTime ShowTime { get; set; }
    public string SeatLabel { get; set; } = string.Empty;
    public string TicketType { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? QrCode { get; set; }
    public bool IsUsed { get; set; }
}

public class InvoiceFoodDTO
{
    public string FoodName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}
