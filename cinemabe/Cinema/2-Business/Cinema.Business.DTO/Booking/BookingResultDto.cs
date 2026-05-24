using Cinema.Data.Enums;
namespace Cinema.Business.DTO.Booking;
public class BookingResultDTO
{
    public Guid InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public InvoiceStatus Status { get; set; }
    public string? PaymentUrl { get; set; }
    public List<TicketItemDTO> Tickets { get; set; } = new();
}

public class TicketItemDTO
{
    public string SeatLabel { get; set; } = string.Empty;
    public string SeatType { get; set; } = string.Empty;
    public string TicketType { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? QrCode { get; set; }
}
