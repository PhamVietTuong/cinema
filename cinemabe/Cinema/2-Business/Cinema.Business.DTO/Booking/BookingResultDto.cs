using Cinema.Data.Enums;
namespace Cinema.Business.DTO.Booking;
public class BookingResultDTO
{
    public Guid InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public double TotalAmount { get; set; }
    public double DiscountAmount { get; set; }
    public double FinalAmount { get; set; }
    /// <summary>Loyalty points spent on this booking (part of DiscountAmount).</summary>
    public int PointsRedeemed { get; set; }
    public InvoiceStatus Status { get; set; }
    public string? PaymentUrl { get; set; }
    public List<TicketItemDTO> Tickets { get; set; } = new();
}

public class TicketItemDTO
{
    public string SeatLabel { get; set; } = string.Empty;
    public string SeatType { get; set; } = string.Empty;
    public double Price { get; set; }
    public string PatronCategory { get; set; } = string.Empty;
    public double PatronDiscountPercent { get; set; }
    public string? QrCode { get; set; }
}
