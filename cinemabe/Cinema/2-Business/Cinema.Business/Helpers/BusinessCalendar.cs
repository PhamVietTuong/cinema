namespace Cinema.Business.Helpers;

/// <summary>
/// The one definition of "local time" and "business day" for staff money reports (decision D4): the theater's local
/// time is Asia/Ho_Chi_Minh (UTC+7, no daylight saving) and a business day starts at a cut-off hour (default 06:00),
/// so a late show still counts toward the previous day. Timestamps such as <c>PaidAt</c> are stored in UTC.
/// </summary>
public static class BusinessCalendar
{
    public const int DefaultCutoffHour = 6;

    private static readonly Lazy<TimeZoneInfo> _zone = new(ResolveZone);

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // try the next id (IANA on Linux/macOS, Windows id otherwise)
            }
            catch (InvalidTimeZoneException)
            {
                // try the next id
            }
        }
        return TimeZoneInfo.CreateCustomTimeZone("Asia/Ho_Chi_Minh", TimeSpan.FromHours(7), "Asia/Ho_Chi_Minh", "Asia/Ho_Chi_Minh");
    }

    /// <summary>A UTC instant as theater local (Asia/Ho_Chi_Minh) wall-clock time.</summary>
    public static DateTime ToLocal(DateTime utc)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone.Value);
    }

    public static DateTime LocalNow(TimeProvider clock)
    {
        return ToLocal(clock.GetUtcNow().UtcDateTime);
    }

    /// <summary>The business date (date only) a UTC instant belongs to.</summary>
    public static DateTime BusinessDateOf(DateTime utc, int cutoffHour)
    {
        return ToLocal(utc).AddHours(-cutoffHour).Date;
    }

    /// <summary>The half-open UTC window [from, to) of a business date.</summary>
    public static (DateTime FromUtc, DateTime ToUtc) WindowOf(DateTime businessDate, int cutoffHour)
    {
        var localStart = DateTime.SpecifyKind(businessDate.Date.AddHours(cutoffHour), DateTimeKind.Unspecified);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, _zone.Value);
        return (fromUtc, fromUtc.AddDays(1));
    }
}
