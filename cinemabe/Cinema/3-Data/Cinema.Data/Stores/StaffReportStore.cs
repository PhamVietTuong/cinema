using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

/// <summary>
/// Management reports. Everything is a SQL GroupBy over untracked projections; the number of queries depends only on
/// the report, never on the number of rows (see the per-method counts on <see cref="IStaffReportStore"/>).
/// </summary>
public class StaffReportStore : IStaffReportStore
{
    private readonly CinemaContext _db;

    public StaffReportStore(CinemaContext db)
    {
        _db = db;
    }

    private IQueryable<Invoice> InvoicesFor(SalesQuery query, bool refunds)
    {
        var from = query.FromUtc;
        var to = query.ToUtc;
        var invoices = _db.Invoice.AsNoTracking();
        if (query.TheaterIds != null)
        {
            var theaterIds = query.TheaterIds;
            invoices = invoices.Where(i => i.TheaterId != null && theaterIds.Contains(i.TheaterId.Value));
        }

        if (refunds)
        {
            return invoices.Where(i => i.Status == InvoiceStatus.Refunded && i.RefundedAt >= from && i.RefundedAt < to);
        }
        return invoices.Where(i => (i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.Refunded)
            && i.PaidAt >= from && i.PaidAt < to);
    }

    public async Task<SalesAggregates> GetSalesAsync(SalesQuery query)
    {
        var aggregates = new SalesAggregates
        {
            Sold = await AggregateAsync(query, refunds: false),
            Refunded = await AggregateAsync(query, refunds: true)
        };
        if (query.GroupBy is SalesGroupBy.Movie or SalesGroupBy.PaymentMethod)
        {
            // An invoice can sit in several groups (two movies, two tenders), so the real totals are counted apart.
            aggregates.SoldInvoices = await InvoicesFor(query, refunds: false).CountAsync();
            aggregates.RefundedInvoices = await InvoicesFor(query, refunds: true).CountAsync();
        }
        return aggregates;
    }

    private async Task<List<SalesAggregateRow>> AggregateAsync(SalesQuery query, bool refunds)
    {
        var invoices = InvoicesFor(query, refunds);
        var shift = query.DayShiftMinutes;
        switch (query.GroupBy)
        {
            case SalesGroupBy.Movie:
                return await AggregateByMovieAsync(invoices);
            case SalesGroupBy.PaymentMethod:
                return await AggregateByTenderAsync(invoices);
            case SalesGroupBy.Day when refunds:
                return await AggregateByInvoiceAsync<DateTime?>(invoices, i => i.RefundedAt!.Value.AddMinutes(shift).Date,
                    (row, key) => row.DayKey = key);
            case SalesGroupBy.Day:
                return await AggregateByInvoiceAsync<DateTime?>(invoices, i => i.PaidAt!.Value.AddMinutes(shift).Date,
                    (row, key) => row.DayKey = key);
            case SalesGroupBy.Theater:
                return await AggregateByInvoiceAsync<Guid?>(invoices, i => i.TheaterId, (row, key) => row.GuidKey = key);
            case SalesGroupBy.Staff:
                return await AggregateByInvoiceAsync<Guid?>(invoices, i => i.SoldByUserId, (row, key) => row.GuidKey = key);
            case SalesGroupBy.Channel:
                return await AggregateByInvoiceAsync<int?>(invoices, i => (int)i.Channel, (row, key) => row.IntKey = key);
            default:
                throw new InvalidOperationException($"{query.GroupBy} is not a sales dimension.");
        }
    }

    /// <summary>
    /// Day / Theater / Staff / Channel: invoice totals plus ticket and F&amp;B line totals (3 queries). The key is a plain
    /// scalar selected from the invoice; the line queries reuse it through the line's Invoice navigation and filter with
    /// an EXISTS over the same invoice query, so all grouping happens in SQL.
    /// </summary>
    private async Task<List<SalesAggregateRow>> AggregateByInvoiceAsync<TKey>(IQueryable<Invoice> invoices,
        Expression<Func<Invoice, TKey>> keySelector, Action<SalesAggregateRow, TKey> assignKey)
    {
        var invoiceRows = await invoices
            .GroupBy(keySelector)
            .Select(g => new
            {
                Key = g.Key,
                InvoiceCount = g.Count(),
                FinalAmount = g.Sum(i => i.FinalAmount),
                DiscountAmount = g.Sum(i => i.DiscountAmount)
            })
            .ToListAsync();

        var ticketKey = Rebind<InvoiceTicket, TKey>(keySelector, t => t.Invoice);
        var ticketRows = await _db.InvoiceTicket.AsNoTracking()
            .Where(t => invoices.Any(i => i.Id == t.InvoiceId))
            .GroupBy(ticketKey)
            .Select(g => new { Key = g.Key, Amount = g.Sum(t => t.Price) })
            .ToListAsync();

        var foodKey = Rebind<InvoiceFoodAndDrink, TKey>(keySelector, f => f.Invoice);
        var foodRows = await _db.InvoiceFoodAndDrink.AsNoTracking()
            .Where(f => invoices.Any(i => i.Id == f.InvoiceId))
            .GroupBy(foodKey)
            .Select(g => new { Key = g.Key, Amount = g.Sum(f => f.TotalPrice) })
            .ToListAsync();

        // ValueTuple wrapper so a null key (e.g. an invoice without a theater) is a valid dictionary key.
        var merged = new Dictionary<ValueTuple<TKey>, SalesAggregateRow>();
        foreach (var r in invoiceRows)
        {
            var row = new SalesAggregateRow
            {
                InvoiceCount = r.InvoiceCount,
                FinalAmount = r.FinalAmount,
                DiscountAmount = r.DiscountAmount
            };
            assignKey(row, r.Key);
            merged[ValueTuple.Create(r.Key)] = row;
        }
        foreach (var r in ticketRows)
        {
            merged[ValueTuple.Create(r.Key)].TicketAmount = r.Amount;
        }
        foreach (var r in foodRows)
        {
            merged[ValueTuple.Create(r.Key)].FoodAmount = r.Amount;
        }
        return merged.Values.ToList();
    }

    /// <summary>Re-expresses an invoice key selector over a line entity by substituting the line's Invoice navigation for the parameter.</summary>
    private static Expression<Func<TLine, TKey>> Rebind<TLine, TKey>(Expression<Func<Invoice, TKey>> keySelector,
        Expression<Func<TLine, Invoice>> navigation)
    {
        var body = new ParameterRebinder(keySelector.Parameters[0], navigation.Body).Visit(keySelector.Body);
        return Expression.Lambda<Func<TLine, TKey>>(body, navigation.Parameters);
    }

    private sealed class ParameterRebinder : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly Expression _replacement;

        public ParameterRebinder(ParameterExpression parameter, Expression replacement)
        {
            _parameter = parameter;
            _replacement = replacement;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _parameter ? _replacement : base.VisitParameter(node);
        }
    }

    /// <summary>
    /// Movie: one row per movie (ticket lines only, food has no movie) plus a movie-less row (null key) that carries
    /// everything a movie cannot: the F&amp;B lines, the invoice discounts and the food-only invoices, so the rows add
    /// up to the invoice totals. 5 queries.
    /// </summary>
    private async Task<List<SalesAggregateRow>> AggregateByMovieAsync(IQueryable<Invoice> invoices)
    {
        var rows = await (from t in _db.InvoiceTicket
                          join i in invoices on t.InvoiceId equals i.Id
                          select new { MovieId = t.ShowTimeRoom.ShowTime.MovieId, t.InvoiceId, t.Price })
            .GroupBy(x => x.MovieId)
            .Select(g => new SalesAggregateRow
            {
                GuidKey = g.Key,
                InvoiceCount = g.Select(x => x.InvoiceId).Distinct().Count(),
                TicketAmount = g.Sum(x => x.Price),
                FinalAmount = g.Sum(x => x.Price)
            })
            .ToListAsync();

        var totals = await invoices
            .GroupBy(i => 1)
            .Select(g => new { Final = g.Sum(i => i.FinalAmount), Discount = g.Sum(i => i.DiscountAmount) })
            .FirstOrDefaultAsync();
        var food = await _db.InvoiceFoodAndDrink.AsNoTracking()
            .Where(f => invoices.Any(i => i.Id == f.InvoiceId))
            .GroupBy(f => 1)
            .Select(g => g.Sum(f => f.TotalPrice))
            .FirstOrDefaultAsync();
        var foodOnlyInvoices = await invoices.CountAsync(i => !i.InvoiceTickets.Any());

        var remainder = (totals?.Final ?? 0) - rows.Sum(r => r.FinalAmount);
        if (foodOnlyInvoices > 0 || food != 0 || remainder != 0)
        {
            rows.Add(new SalesAggregateRow
            {
                InvoiceCount = foodOnlyInvoices,
                FoodAmount = food,
                DiscountAmount = totals?.Discount ?? 0,
                FinalAmount = remainder
            });
        }
        return rows;
    }

    /// <summary>
    /// PaymentMethod: the per-tender rows (what each tender contributed to the invoices) plus a tender-less row (null
    /// key) for invoices with no payment lines or whose lines do not cover the final amount, so the rows add up to
    /// the invoice totals. 3 queries.
    /// </summary>
    private async Task<List<SalesAggregateRow>> AggregateByTenderAsync(IQueryable<Invoice> invoices)
    {
        var rows = await (from p in _db.InvoicePayment
                          join i in invoices on p.InvoiceId equals i.Id
                          select new { p.Method, p.InvoiceId, p.Amount })
            .GroupBy(x => x.Method)
            .Select(g => new SalesAggregateRow
            {
                IntKey = (int)g.Key,
                InvoiceCount = g.Select(x => x.InvoiceId).Distinct().Count(),
                FinalAmount = g.Sum(x => x.Amount)
            })
            .ToListAsync();

        var totalFinal = await invoices
            .GroupBy(i => 1)
            .Select(g => g.Sum(i => i.FinalAmount))
            .FirstOrDefaultAsync();
        var withoutPayment = await invoices.CountAsync(i => !i.Payments.Any());

        var remainder = totalFinal - rows.Sum(r => r.FinalAmount);
        if (withoutPayment > 0 || remainder != 0)
        {
            rows.Add(new SalesAggregateRow { InvoiceCount = withoutPayment, FinalAmount = remainder });
        }
        return rows;
    }

    public async Task<Dictionary<Guid, string>> GetMovieTitlesAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }
        return await _db.Movie.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Title);
    }

    public async Task<Dictionary<Guid, string>> GetTheaterNamesAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }
        return await _db.Theater.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name);
    }

    public async Task<List<OccupancyRow>> GetOccupancyAsync(OccupancyQuery query)
    {
        var from = query.FromLocal;
        var to = query.ToLocal;

        var screenings = _db.ShowTimeRoom.AsNoTracking()
            .Where(sr => sr.ShowTime.IsActive && sr.ShowTime.StartTime >= from && sr.ShowTime.StartTime < to);
        var soldTickets = _db.InvoiceTicket
            .Where(t => t.IsActive && t.Invoice.Status == InvoiceStatus.Paid
                && t.ShowTimeRoom.ShowTime.StartTime >= from && t.ShowTimeRoom.ShowTime.StartTime < to);
        var seats = _db.Seat.Where(s => s.IsActive);
        if (query.TheaterIds != null)
        {
            var theaterIds = query.TheaterIds;
            screenings = screenings.Where(sr => theaterIds.Contains(sr.Room.TheaterId));
            soldTickets = soldTickets.Where(t => theaterIds.Contains(t.ShowTimeRoom.Room.TheaterId));
            seats = seats.Where(s => theaterIds.Contains(s.Room.TheaterId));
        }

        var rows = await screenings
            .OrderBy(sr => sr.ShowTime.StartTime)
            .ThenBy(sr => sr.Room.Name)
            .Select(sr => new OccupancyRow
            {
                ShowTimeId = sr.ShowTimeId,
                RoomId = sr.RoomId,
                TheaterId = sr.Room.TheaterId,
                TheaterName = sr.Room.Theater.Name,
                RoomName = sr.Room.Name,
                MovieTitle = sr.ShowTime.Movie.Title,
                StartTime = sr.ShowTime.StartTime
            })
            .ToListAsync();

        var sold = await soldTickets
            .GroupBy(t => new { t.ShowTimeId, t.RoomId })
            .Select(g => new { g.Key.ShowTimeId, g.Key.RoomId, Count = g.Count() })
            .ToListAsync();
        var soldByScreening = sold.ToDictionary(s => (s.ShowTimeId, s.RoomId), s => s.Count);

        var activeSeats = await seats
            .GroupBy(s => s.RoomId)
            .Select(g => new { RoomId = g.Key, Count = g.Count() })
            .ToListAsync();
        var seatsByRoom = activeSeats.ToDictionary(s => s.RoomId, s => s.Count);

        foreach (var row in rows)
        {
            row.SoldSeats = soldByScreening.TryGetValue((row.ShowTimeId, row.RoomId), out var soldCount) ? soldCount : 0;
            row.ActiveSeats = seatsByRoom.TryGetValue(row.RoomId, out var seatCount) ? seatCount : 0;
        }
        return rows;
    }

    public async Task<KpiRaw> GetKpiRawAsync(SalesQuery query)
    {
        var sold = await InvoicesFor(query, refunds: false)
            .GroupBy(i => 1)
            .Select(g => new { Count = g.Count(), Amount = g.Sum(i => i.FinalAmount) })
            .FirstOrDefaultAsync();
        var refunded = await InvoicesFor(query, refunds: true)
            .GroupBy(i => 1)
            .Select(g => new { Count = g.Count(), Amount = g.Sum(i => i.FinalAmount) })
            .FirstOrDefaultAsync();

        // Currently-Paid invoices of the window (refunded ones are excluded from the basket metrics).
        var paid = InvoicesFor(query, refunds: false).Where(i => i.Status == InvoiceStatus.Paid);
        var withTickets = paid.Where(i => i.InvoiceTickets.Any());
        var ticketInvoices = await withTickets
            .GroupBy(i => 1)
            .Select(g => new { Count = g.Count(), Amount = g.Sum(i => i.FinalAmount) })
            .FirstOrDefaultAsync();
        var withFood = await withTickets.CountAsync(i => i.InvoiceFoodAndDrinks.Any());
        var tickets = await (from t in _db.InvoiceTicket
                             join i in withTickets on t.InvoiceId equals i.Id
                             select t.InvoiceId).CountAsync();

        return new KpiRaw
        {
            SoldInvoices = sold?.Count ?? 0,
            SoldAmount = sold?.Amount ?? 0,
            RefundedInvoices = refunded?.Count ?? 0,
            RefundedAmount = refunded?.Amount ?? 0,
            TicketInvoices = ticketInvoices?.Count ?? 0,
            TicketInvoicesAmount = ticketInvoices?.Amount ?? 0,
            TicketInvoicesWithFood = withFood,
            Tickets = tickets
        };
    }
}
