using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Staff;

/// <summary>
/// Range + scope of a management report. <see cref="From"/> and <see cref="To"/> are business dates (decision D4:
/// local time with a cut-off hour, so the business day 2026-10-04 runs 06:00 on the 4th to 06:00 on the 5th) and
/// both are inclusive; the range may span at most 92 days.
/// </summary>
public class StaffReportRequest
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    /// <summary>
    /// Theaters to report on. Omit/empty = every theater the caller may see. Naming a theater outside the caller's
    /// scope is refused with 403.
    /// </summary>
    public List<Guid>? TheaterIds { get; set; }

    /// <summary>The dimension to group sales by (ignored by occupancy and KPIs).</summary>
    public SalesGroupBy GroupBy { get; set; } = SalesGroupBy.Day;
}

/// <summary>
/// One group of a sales report. Revenue is net of refunds: sold in the range minus invoices refunded in the range.
/// <see cref="TicketRevenue"/>/<see cref="FoodRevenue"/> are null where the grouping cannot attribute them
/// (food has no movie; a tender covers the whole invoice, not one product line).
/// </summary>
public class SalesReportRowDTO
{
    /// <summary>Stable key: yyyy-MM-dd for Day, an id for Movie/Theater/Staff, the enum name for Channel/PaymentMethod, empty when unattributed.</summary>
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>Invoices paid in the range (including those refunded later).</summary>
    public int InvoiceCount { get; set; }
    public double? TicketRevenue { get; set; }
    public double? FoodRevenue { get; set; }
    /// <summary>Invoice-level discounts (points, gift card, vouchers) net of refunds; null where not attributable.</summary>
    public double? DiscountAmount { get; set; }
    /// <summary>Amount actually taken: (ticket + food - discount) net of refunds.</summary>
    public double NetRevenue { get; set; }
    public int RefundCount { get; set; }
    public double RefundAmount { get; set; }
}

public class SalesReportDTO
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public SalesGroupBy GroupBy { get; set; }
    public List<SalesReportRowDTO> Rows { get; set; } = new();
    /// <summary>Column totals over <see cref="Rows"/> (Key/Label empty).</summary>
    public SalesReportRowDTO Totals { get; set; } = new();
}

public class OccupancyRowDTO
{
    public Guid ShowTimeId { get; set; }
    public Guid RoomId { get; set; }
    public Guid TheaterId { get; set; }
    public string TheaterName { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string MovieTitle { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int SoldSeats { get; set; }
    public int ActiveSeats { get; set; }
    /// <summary>SoldSeats / ActiveSeats, 0 when the room has no active seats.</summary>
    public double OccupancyRate { get; set; }
}

public class OccupancyReportDTO
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<OccupancyRowDTO> Rows { get; set; } = new();
    public int TotalSoldSeats { get; set; }
    public int TotalActiveSeats { get; set; }
    public double OccupancyRate { get; set; }
}

public class StaffKpisDTO
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    /// <summary>Invoices paid in the range (including those refunded later).</summary>
    public int PaidInvoices { get; set; }
    /// <summary>Paid invoices that contain tickets.</summary>
    public int TicketInvoices { get; set; }
    /// <summary>Paid ticket invoices that also contain food or drink.</summary>
    public int TicketInvoicesWithFood { get; set; }
    /// <summary>TicketInvoicesWithFood / TicketInvoices (0 when there are no ticket invoices).</summary>
    public double AttachRate { get; set; }
    public int TicketsSold { get; set; }
    /// <summary>Final amount of paid ticket invoices / tickets sold (0 when none).</summary>
    public double AverageSpendPerHead { get; set; }
    public int RefundCount { get; set; }
    public double RefundAmount { get; set; }
    /// <summary>RefundCount / PaidInvoices.</summary>
    public double RefundRateByCount { get; set; }
    /// <summary>RefundAmount / gross amount of paid invoices.</summary>
    public double RefundRateByAmount { get; set; }
}
