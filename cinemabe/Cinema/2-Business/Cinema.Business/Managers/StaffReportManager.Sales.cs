using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Staff;
using Cinema.Data.Contracts;
using Cinema.Data.Enums;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Managers;

/// <summary>P9 management reports: sales, occupancy and KPIs.</summary>
public partial class StaffReportManager
{
    private const int _maxRangeDays = 92;
    private const string _dayCutoffHourKey = "Business:DayCutoffHour";
    private const string _timeZoneIdKey = "Business:TimeZoneId";
    private const int _defaultDayCutoffHour = 6;
    private const string _defaultTimeZoneId = "Asia/Ho_Chi_Minh";
    private const string _windowsVietnamTimeZoneId = "SE Asia Standard Time";
    private const string _dayKeyFormat = "yyyy-MM-dd";
    private const string _unattributedLabel = "Unattributed";

    /// <summary>The business-day range of a request in UTC (invoice events), local (screenings) and the SQL day shift.</summary>
    private sealed record ReportWindow(
        DateTime From,
        DateTime To,
        DateTime FromLocal,
        DateTime ToLocal,
        DateTime FromUtc,
        DateTime ToUtc,
        int DayShiftMinutes);

    public async Task<SalesReportDTO> GetSalesAsync(StaffReportRequest request, IReadOnlyCollection<Guid>? scope)
    {
        var window = BuildWindow(request);
        var theaterIds = ResolveTheaterIds(request, scope);
        var report = new SalesReportDTO { From = window.From, To = window.To, GroupBy = request.GroupBy };
        if (theaterIds != null && theaterIds.Count == 0)
        {
            return report;
        }

        var aggregates = await _uow.StaffReportStore.GetSalesAsync(ToQuery(window, theaterIds, request.GroupBy));

        var rows = new Dictionary<string, SalesReportRowDTO>();
        foreach (var sold in aggregates.Sold)
        {
            var row = GetOrAdd(rows, KeyOf(sold, request.GroupBy));
            row.InvoiceCount += sold.InvoiceCount;
            row.TicketRevenue = (row.TicketRevenue ?? 0) + sold.TicketAmount;
            row.FoodRevenue = (row.FoodRevenue ?? 0) + sold.FoodAmount;
            row.DiscountAmount = (row.DiscountAmount ?? 0) + sold.DiscountAmount;
            row.NetRevenue += sold.FinalAmount;
        }
        foreach (var refunded in aggregates.Refunded)
        {
            var row = GetOrAdd(rows, KeyOf(refunded, request.GroupBy));
            row.RefundCount += refunded.InvoiceCount;
            row.RefundAmount += refunded.FinalAmount;
            row.TicketRevenue = (row.TicketRevenue ?? 0) - refunded.TicketAmount;
            row.FoodRevenue = (row.FoodRevenue ?? 0) - refunded.FoodAmount;
            row.DiscountAmount = (row.DiscountAmount ?? 0) - refunded.DiscountAmount;
            row.NetRevenue -= refunded.FinalAmount;
        }

        if (request.GroupBy == SalesGroupBy.Day)
        {
            for (var day = window.From.Date; day <= window.To.Date; day = day.AddDays(1))
            {
                var key = day.ToString(_dayKeyFormat);
                if (!rows.ContainsKey(key))
                {
                    rows[key] = new SalesReportRowDTO { Key = key, Label = key, TicketRevenue = 0, FoodRevenue = 0, DiscountAmount = 0 };
                }
            }
        }

        await FillLabelsAsync(rows.Values, request.GroupBy);
        ClearUnattributedMeasures(rows.Values, request.GroupBy);

        report.Rows = request.GroupBy == SalesGroupBy.Day
            ? rows.Values.OrderBy(r => r.Key, StringComparer.Ordinal).ToList()
            : rows.Values.OrderByDescending(r => r.NetRevenue).ThenBy(r => r.Label, StringComparer.Ordinal).ToList();
        report.Totals = SumRows(report.Rows);
        if (aggregates.SoldInvoices.HasValue)
        {
            // One invoice can sit in several groups (two movies, two tenders): the header counts it once.
            report.Totals.InvoiceCount = aggregates.SoldInvoices.Value;
        }
        if (aggregates.RefundedInvoices.HasValue)
        {
            report.Totals.RefundCount = aggregates.RefundedInvoices.Value;
        }
        return report;
    }

    public async Task<OccupancyReportDTO> GetOccupancyAsync(StaffReportRequest request, IReadOnlyCollection<Guid>? scope)
    {
        var window = BuildWindow(request);
        var theaterIds = ResolveTheaterIds(request, scope);
        var report = new OccupancyReportDTO { From = window.From, To = window.To };
        if (theaterIds != null && theaterIds.Count == 0)
        {
            return report;
        }

        var rows = await _uow.StaffReportStore.GetOccupancyAsync(new OccupancyQuery(window.FromLocal, window.ToLocal, theaterIds));
        report.Rows = rows.Select(r => new OccupancyRowDTO
        {
            ShowTimeId = r.ShowTimeId,
            RoomId = r.RoomId,
            TheaterId = r.TheaterId,
            TheaterName = r.TheaterName,
            RoomName = r.RoomName,
            MovieTitle = r.MovieTitle,
            StartTime = r.StartTime,
            SoldSeats = r.SoldSeats,
            ActiveSeats = r.ActiveSeats,
            OccupancyRate = Ratio(r.SoldSeats, r.ActiveSeats)
        }).ToList();
        report.TotalSoldSeats = report.Rows.Sum(r => r.SoldSeats);
        report.TotalActiveSeats = report.Rows.Sum(r => r.ActiveSeats);
        report.OccupancyRate = Ratio(report.TotalSoldSeats, report.TotalActiveSeats);
        return report;
    }

    public async Task<StaffKpisDTO> GetKpisAsync(StaffReportRequest request, IReadOnlyCollection<Guid>? scope)
    {
        var window = BuildWindow(request);
        var theaterIds = ResolveTheaterIds(request, scope);
        var kpis = new StaffKpisDTO { From = window.From, To = window.To };
        if (theaterIds != null && theaterIds.Count == 0)
        {
            return kpis;
        }

        var raw = await _uow.StaffReportStore.GetKpiRawAsync(ToQuery(window, theaterIds, SalesGroupBy.Day));
        kpis.PaidInvoices = raw.SoldInvoices;
        kpis.TicketInvoices = raw.TicketInvoices;
        kpis.TicketInvoicesWithFood = raw.TicketInvoicesWithFood;
        kpis.AttachRate = Ratio(raw.TicketInvoicesWithFood, raw.TicketInvoices);
        kpis.TicketsSold = raw.Tickets;
        kpis.AverageSpendPerHead = raw.Tickets == 0 ? 0 : raw.TicketInvoicesAmount / raw.Tickets;
        kpis.RefundCount = raw.RefundedInvoices;
        kpis.RefundAmount = raw.RefundedAmount;
        kpis.RefundRateByCount = Ratio(raw.RefundedInvoices, raw.SoldInvoices);
        kpis.RefundRateByAmount = raw.SoldAmount == 0 ? 0 : raw.RefundedAmount / raw.SoldAmount;
        return kpis;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static double Ratio(int numerator, int denominator)
    {
        return denominator == 0 ? 0 : (double)numerator / denominator;
    }

    private static SalesQuery ToQuery(ReportWindow window, IReadOnlyCollection<Guid>? theaterIds, SalesGroupBy groupBy)
    {
        return new SalesQuery(window.FromUtc, window.ToUtc, theaterIds, window.DayShiftMinutes, groupBy);
    }

    /// <summary>
    /// The theaters to report on: the requested ones when given (each must be inside <paramref name="scope"/>, else
    /// 403), otherwise the whole scope. Null = every theater.
    /// </summary>
    private static IReadOnlyCollection<Guid>? ResolveTheaterIds(StaffReportRequest request, IReadOnlyCollection<Guid>? scope)
    {
        var requested = (request.TheaterIds ?? new List<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (requested.Count == 0)
        {
            return scope;
        }
        if (scope != null && requested.Any(id => !scope.Contains(id)))
        {
            throw new AccessDeniedException("The requested theater is outside your scope.");
        }
        return requested;
    }

    private ReportWindow BuildWindow(StaffReportRequest request)
    {
        if (request == null)
        {
            throw new InvalidOperationException("A date range is required.");
        }
        var from = request.From.Date;
        var to = request.To.Date;
        if (to < from)
        {
            throw new InvalidOperationException("The end date must not be before the start date.");
        }
        if ((to - from).Days + 1 > _maxRangeDays)
        {
            throw new InvalidOperationException($"The report range is limited to {_maxRangeDays} days.");
        }

        var cutoffHour = int.TryParse(_config[_dayCutoffHourKey], out var configured) && configured is >= 0 and <= 23
            ? configured
            : _defaultDayCutoffHour;
        var zone = ResolveTimeZone();

        var fromLocal = DateTime.SpecifyKind(from.AddHours(cutoffHour), DateTimeKind.Unspecified);
        var toLocal = DateTime.SpecifyKind(to.AddDays(1).AddHours(cutoffHour), DateTimeKind.Unspecified);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromLocal, zone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toLocal, zone);
        var shift = (int)zone.GetUtcOffset(fromUtc).TotalMinutes - cutoffHour * 60;
        return new ReportWindow(from, to, fromLocal, toLocal, fromUtc, toUtc, shift);
    }

    /// <summary>Decision D4: the business day runs in Asia/Ho_Chi_Minh (Windows id as fallback, then the server zone).</summary>
    private TimeZoneInfo ResolveTimeZone()
    {
        var configuredId = _config[_timeZoneIdKey];
        foreach (var id in new[] { configuredId, _defaultTimeZoneId, _windowsVietnamTimeZoneId })
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the next candidate.
            }
            catch (InvalidTimeZoneException)
            {
                // Try the next candidate.
            }
        }
        return _clock.LocalTimeZone;
    }

    private static string KeyOf(SalesAggregateRow row, SalesGroupBy groupBy)
    {
        switch (groupBy)
        {
            case SalesGroupBy.Day:
                return row.DayKey?.ToString(_dayKeyFormat) ?? string.Empty;
            case SalesGroupBy.Channel:
                return row.IntKey.HasValue ? ((SalesChannel)row.IntKey.Value).ToString() : string.Empty;
            case SalesGroupBy.PaymentMethod:
                return row.IntKey.HasValue ? ((PaymentTender)row.IntKey.Value).ToString() : string.Empty;
            default:
                return row.GuidKey?.ToString() ?? string.Empty;
        }
    }

    private static SalesReportRowDTO GetOrAdd(Dictionary<string, SalesReportRowDTO> rows, string key)
    {
        if (!rows.TryGetValue(key, out var row))
        {
            row = new SalesReportRowDTO { Key = key, Label = key };
            rows[key] = row;
        }
        return row;
    }

    /// <summary>Resolves display names for Movie/Theater/Staff keys: one batched lookup each.</summary>
    private async Task FillLabelsAsync(IEnumerable<SalesReportRowDTO> rows, SalesGroupBy groupBy)
    {
        var list = rows.ToList();
        if (groupBy == SalesGroupBy.PaymentMethod)
        {
            foreach (var row in list.Where(r => r.Key.Length == 0))
            {
                row.Label = _unattributedLabel;
            }
            return;
        }
        if (groupBy is not (SalesGroupBy.Movie or SalesGroupBy.Theater or SalesGroupBy.Staff))
        {
            return;
        }

        var ids = list.Where(r => r.Key.Length > 0).Select(r => Guid.Parse(r.Key)).ToList();
        Dictionary<Guid, string> names;
        switch (groupBy)
        {
            case SalesGroupBy.Movie:
                names = await _uow.StaffReportStore.GetMovieTitlesAsync(ids);
                break;
            case SalesGroupBy.Theater:
                names = await _uow.StaffReportStore.GetTheaterNamesAsync(ids);
                break;
            default:
                names = await _uow.UserStore.GetNamesByIdsAsync(ids);
                break;
        }

        foreach (var row in list)
        {
            if (row.Key.Length == 0)
            {
                row.Label = _unattributedLabel;
            }
            else if (names.TryGetValue(Guid.Parse(row.Key), out var name))
            {
                row.Label = name;
            }
        }
    }

    /// <summary>
    /// Movie rows know ticket revenue only (the movie-less row keeps the F&amp;B and discount); tender rows know neither
    /// split (see <see cref="SalesReportRowDTO"/>).
    /// </summary>
    private static void ClearUnattributedMeasures(IEnumerable<SalesReportRowDTO> rows, SalesGroupBy groupBy)
    {
        foreach (var row in rows)
        {
            switch (groupBy)
            {
                case SalesGroupBy.Movie when row.Key.Length > 0:
                    row.FoodRevenue = null;
                    row.DiscountAmount = null;
                    break;
                case SalesGroupBy.PaymentMethod:
                    row.TicketRevenue = null;
                    row.FoodRevenue = null;
                    row.DiscountAmount = null;
                    break;
            }
        }
    }

    private static SalesReportRowDTO SumRows(IReadOnlyCollection<SalesReportRowDTO> rows)
    {
        return new SalesReportRowDTO
        {
            InvoiceCount = rows.Sum(r => r.InvoiceCount),
            TicketRevenue = rows.Any(r => r.TicketRevenue.HasValue) ? rows.Sum(r => r.TicketRevenue ?? 0) : null,
            FoodRevenue = rows.Any(r => r.FoodRevenue.HasValue) ? rows.Sum(r => r.FoodRevenue ?? 0) : null,
            DiscountAmount = rows.Any(r => r.DiscountAmount.HasValue) ? rows.Sum(r => r.DiscountAmount ?? 0) : null,
            NetRevenue = rows.Sum(r => r.NetRevenue),
            RefundCount = rows.Sum(r => r.RefundCount),
            RefundAmount = rows.Sum(r => r.RefundAmount)
        };
    }
}
