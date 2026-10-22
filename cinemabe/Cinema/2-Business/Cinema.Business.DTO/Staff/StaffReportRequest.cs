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
