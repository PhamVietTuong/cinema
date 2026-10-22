using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Staff;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.BoxOffice;

public class CounterQuoteDTO
{
    public List<CounterQuoteLineDTO> Lines { get; set; } = new();
    public double TotalAmount { get; set; }
    /// <summary>Membership and promo discounts only (points and gift card are reported separately).</summary>
    public double DiscountAmount { get; set; }
    public double PointsValue { get; set; }
    public double GiftCardAmount { get; set; }
    /// <summary>What remains to be tendered.</summary>
    public double FinalAmount { get; set; }
}
