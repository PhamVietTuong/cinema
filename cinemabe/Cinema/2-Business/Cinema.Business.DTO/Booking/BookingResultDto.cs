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
    /// <summary>Title of the auto-apply PromotionProgram that produced this price, if any (null when a
    /// Discount — code or auto-apply — was the cheaper source instead, or neither applied).</summary>
    public string? AppliedPromotionTitle { get; set; }
    public List<TicketItemDTO> Tickets { get; set; } = new();
}

public class TicketItemDTO
{
    public string SeatLabel { get; set; } = string.Empty;
    /// <summary>"Single" or "Double".</summary>
    public string SeatType { get; set; } = string.Empty;
    public double Price { get; set; }
    public string PatronCategory { get; set; } = string.Empty;
    public string? QrCode { get; set; }
}
