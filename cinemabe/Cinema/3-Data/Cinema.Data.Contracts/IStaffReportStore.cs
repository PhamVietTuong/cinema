using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>
/// A sales-report window. <see cref="FromUtc"/>/<see cref="ToUtc"/> bound invoice events (PaidAt / RefundedAt, stored
/// UTC), inclusive/exclusive. <see cref="DayShiftMinutes"/> moves a UTC instant onto the business-day clock
/// (local offset minus the day cut-off) so that <c>AddMinutes(shift).Date</c> is the business day.
/// <see cref="TheaterIds"/> null = every theater.
/// </summary>
public record SalesQuery(
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyCollection<Guid>? TheaterIds,
    int DayShiftMinutes,
    SalesGroupBy GroupBy);

/// <summary>
/// One group of a sales aggregate. Exactly one of the key fields is filled, depending on the dimension:
/// <see cref="DayKey"/> (Day), <see cref="GuidKey"/> (Theater, Staff, Movie) or <see cref="IntKey"/> (Channel,
/// PaymentMethod). Measures that a dimension cannot attribute are left 0.
/// </summary>
public class SalesAggregateRow
{
    public DateTime? DayKey { get; set; }
    public Guid? GuidKey { get; set; }
    public int? IntKey { get; set; }
    public int InvoiceCount { get; set; }
    public double TicketAmount { get; set; }
    public double FoodAmount { get; set; }
    public double FinalAmount { get; set; }
    public double DiscountAmount { get; set; }
}

/// <summary>Sold = invoices paid in the window (Paid or later Refunded); Refunded = invoices refunded in the window.</summary>
public class SalesAggregates
{
    public List<SalesAggregateRow> Sold { get; set; } = new();
    public List<SalesAggregateRow> Refunded { get; set; } = new();
}

/// <summary>Showtime-room screenings whose local start lies in [From, To), optionally limited to theaters.</summary>
public record OccupancyQuery(DateTime FromLocal, DateTime ToLocal, IReadOnlyCollection<Guid>? TheaterIds);

public class OccupancyRow
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
}

public class KpiRaw
{
    /// <summary>Invoices paid in the window (including those refunded later) and their final amounts.</summary>
    public int SoldInvoices { get; set; }
    public double SoldAmount { get; set; }
    /// <summary>Invoices refunded in the window and their final amounts.</summary>
    public int RefundedInvoices { get; set; }
    public double RefundedAmount { get; set; }
    /// <summary>Currently Paid invoices (paid in the window) that contain at least one ticket, and their final amounts.</summary>
    public int TicketInvoices { get; set; }
    public double TicketInvoicesAmount { get; set; }
    /// <summary>Of those, how many also contain food or drink.</summary>
    public int TicketInvoicesWithFood { get; set; }
    /// <summary>Tickets on those invoices (the head count).</summary>
    public int Tickets { get; set; }
}

/// <summary>Read-only management reports. Every method issues a fixed number of queries regardless of row count.</summary>
public interface IStaffReportStore
{
    /// <summary>Sales grouped by <see cref="SalesQuery.GroupBy"/>: at most 6 grouped queries.</summary>
    Task<SalesAggregates> GetSalesAsync(SalesQuery query);

    /// <summary>Movie titles by id (one query). Unknown ids are absent.</summary>
    Task<Dictionary<Guid, string>> GetMovieTitlesAsync(IReadOnlyCollection<Guid> ids);

    /// <summary>Theater names by id (one query). Unknown ids are absent.</summary>
    Task<Dictionary<Guid, string>> GetTheaterNamesAsync(IReadOnlyCollection<Guid> ids);

    /// <summary>Per showtime-room sold/active seats: 3 queries (screenings, sold tickets, active seats).</summary>
    Task<List<OccupancyRow>> GetOccupancyAsync(OccupancyQuery query);

    /// <summary>The KPI building blocks: 5 queries.</summary>
    Task<KpiRaw> GetKpiRawAsync(SalesQuery query);
}
