using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>
/// A sales-report window. <see cref="FromUtc"/>/<see cref="ToUtc"/> bound invoice events (PaidAt, stored
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
