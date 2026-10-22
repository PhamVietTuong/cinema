using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.BoxOffice;

public class CounterSaleResultDTO
{
    public Guid InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public double TotalAmount { get; set; }
    /// <summary>Membership and promo discounts only.</summary>
    public double DiscountAmount { get; set; }
    public double PointsValue { get; set; }
    public double GiftCardAmount { get; set; }
    public double FinalAmount { get; set; }
    /// <summary>Cash to hand back to the customer.</summary>
    public double ChangeDue { get; set; }
    public DateTime PaidAt { get; set; }
    public List<CounterQuoteLineDTO> Lines { get; set; } = new();
    public List<TicketItemDTO> Tickets { get; set; } = new();
}
