using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Staff;

/// <summary>
/// One group of a sales report: invoices paid in the range.
/// <see cref="TicketRevenue"/>/<see cref="FoodRevenue"/> are null where the grouping cannot attribute them
/// (food has no movie; a tender covers the whole invoice, not one product line). Movie and PaymentMethod
/// groupings also carry one row with an empty <see cref="Key"/> (no movie / F&amp;B, no payment line) so every
/// grouping adds up to the same net revenue for the same period.
/// </summary>
public class SalesReportRowDTO
{
    /// <summary>Stable key: yyyy-MM-dd for Day, an id for Movie/Theater/Staff, the enum name for Channel/PaymentMethod, empty when unattributed.</summary>
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>Invoices paid in the range.</summary>
    public int InvoiceCount { get; set; }
    public double? TicketRevenue { get; set; }
    public double? FoodRevenue { get; set; }
    /// <summary>Invoice-level discounts (points, gift card, vouchers); null where not attributable.</summary>
    public double? DiscountAmount { get; set; }
    /// <summary>Amount actually taken: ticket + food - discount.</summary>
    public double NetRevenue { get; set; }
}
