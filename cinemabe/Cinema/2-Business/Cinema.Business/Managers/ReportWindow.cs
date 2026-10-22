namespace Cinema.Business.Managers;

/// <summary>The business-day range of a request in UTC (invoice events), local (screenings) and the SQL day shift.</summary>
internal sealed record ReportWindow(
    DateTime From,
    DateTime To,
    DateTime FromLocal,
    DateTime ToLocal,
    DateTime FromUtc,
    DateTime ToUtc,
    int DayShiftMinutes);
