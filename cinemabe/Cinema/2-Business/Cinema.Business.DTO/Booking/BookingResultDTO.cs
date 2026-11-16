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
    /// <summary>True when this is the customer's earlier unpaid booking for the same seats, handed back instead of a new
    /// one (nothing was created or reserved again). Its amounts are the ones fixed when it was made, which can differ
    /// from what the customer sees now, so the client must check them before paying.</summary>
    public bool Resumed { get; set; }
    public string? PaymentUrl { get; set; }
    /// <summary>Title of the auto-apply PromotionProgram that produced this price, if any (null when a
    /// Discount — code or auto-apply — was the cheaper source instead, or neither applied).</summary>
    public string? AppliedPromotionTitle { get; set; }
    public List<TicketItemDTO> Tickets { get; set; } = new();
}
