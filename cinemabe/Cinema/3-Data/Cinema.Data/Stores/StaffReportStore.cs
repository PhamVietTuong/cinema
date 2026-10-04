using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
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

    /// <summary>Invoice-level key columns, filled for the single dimension in use.</summary>
    private sealed class KeyedInvoice
    {
        public Guid InvoiceId { get; set; }
        public DateTime? DayKey { get; set; }
        public Guid? GuidKey { get; set; }
        public int? IntKey { get; set; }
        public double Final { get; set; }
        public double Discount { get; set; }
    }

    private sealed class KeyedAmount
    {
        public DateTime? DayKey { get; set; }
        public Guid? GuidKey { get; set; }
        public int? IntKey { get; set; }
        public double Amount { get; set; }
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

    private static IQueryable<KeyedInvoice> Keyed(IQueryable<Invoice> invoices, SalesQuery query, bool refunds)
    {
        var shift = query.DayShiftMinutes;
        switch (query.GroupBy)
        {
            case SalesGroupBy.Day when refunds:
                return invoices.Select(i => new KeyedInvoice
                {
                    InvoiceId = i.Id,
                    DayKey = i.RefundedAt!.Value.AddMinutes(shift).Date,
                    Final = i.FinalAmount,
                    Discount = i.DiscountAmount
                });
            case SalesGroupBy.Day:
                return invoices.Select(i => new KeyedInvoice
                {
                    InvoiceId = i.Id,
                    DayKey = i.PaidAt!.Value.AddMinutes(shift).Date,
                    Final = i.FinalAmount,
                    Discount = i.DiscountAmount
                });
            case SalesGroupBy.Theater:
                return invoices.Select(i => new KeyedInvoice { InvoiceId = i.Id, GuidKey = i.TheaterId, Final = i.FinalAmount, Discount = i.DiscountAmount });
            case SalesGroupBy.Staff:
                return invoices.Select(i => new KeyedInvoice { InvoiceId = i.Id, GuidKey = i.SoldByUserId, Final = i.FinalAmount, Discount = i.DiscountAmount });
            case SalesGroupBy.Channel:
                return invoices.Select(i => new KeyedInvoice { InvoiceId = i.Id, IntKey = (int)i.Channel, Final = i.FinalAmount, Discount = i.DiscountAmount });
            default:
                throw new InvalidOperationException($"{query.GroupBy} is not an invoice-level dimension.");
        }
    }

    public async Task<SalesAggregates> GetSalesAsync(SalesQuery query)
    {
        return new SalesAggregates
        {
            Sold = await AggregateAsync(query, refunds: false),
            Refunded = await AggregateAsync(query, refunds: true)
        };
    }

    private async Task<List<SalesAggregateRow>> AggregateAsync(SalesQuery query, bool refunds)
    {
        var invoices = InvoicesFor(query, refunds);
        switch (query.GroupBy)
        {
            case SalesGroupBy.Movie:
                return await AggregateByMovieAsync(invoices);
            case SalesGroupBy.PaymentMethod:
                return await AggregateByTenderAsync(invoices);
            default:
                return await AggregateByInvoiceAsync(Keyed(invoices, query, refunds));
        }
    }

    /// <summary>Day / Theater / Staff / Channel: invoice totals plus ticket and F&amp;B line totals (3 queries).</summary>
    private async Task<List<SalesAggregateRow>> AggregateByInvoiceAsync(IQueryable<KeyedInvoice> keyed)
    {
        var invoiceRows = await keyed
            .GroupBy(k => new { k.DayKey, k.GuidKey, k.IntKey })
            .Select(g => new SalesAggregateRow
            {
                DayKey = g.Key.DayKey,
                GuidKey = g.Key.GuidKey,
                IntKey = g.Key.IntKey,
                InvoiceCount = g.Count(),
                FinalAmount = g.Sum(k => k.Final),
                DiscountAmount = g.Sum(k => k.Discount)
            })
            .ToListAsync();

        var ticketRows = await keyed
            .Join(_db.InvoiceTicket, k => k.InvoiceId, t => t.InvoiceId,
                (k, t) => new KeyedAmount { DayKey = k.DayKey, GuidKey = k.GuidKey, IntKey = k.IntKey, Amount = t.Price })
            .GroupBy(x => new { x.DayKey, x.GuidKey, x.IntKey })
            .Select(g => new SalesAggregateRow
            {
                DayKey = g.Key.DayKey,
                GuidKey = g.Key.GuidKey,
                IntKey = g.Key.IntKey,
                TicketAmount = g.Sum(x => x.Amount)
            })
            .ToListAsync();

        var foodRows = await keyed
            .Join(_db.InvoiceFoodAndDrink, k => k.InvoiceId, f => f.InvoiceId,
                (k, f) => new KeyedAmount { DayKey = k.DayKey, GuidKey = k.GuidKey, IntKey = k.IntKey, Amount = f.TotalPrice })
            .GroupBy(x => new { x.DayKey, x.GuidKey, x.IntKey })
            .Select(g => new SalesAggregateRow
            {
                DayKey = g.Key.DayKey,
                GuidKey = g.Key.GuidKey,
                IntKey = g.Key.IntKey,
                FoodAmount = g.Sum(x => x.Amount)
            })
            .ToListAsync();

        var merged = invoiceRows.ToDictionary(r => (r.DayKey, r.GuidKey, r.IntKey));
        foreach (var row in ticketRows)
        {
            merged[(row.DayKey, row.GuidKey, row.IntKey)].TicketAmount = row.TicketAmount;
        }
        foreach (var row in foodRows)
        {
            merged[(row.DayKey, row.GuidKey, row.IntKey)].FoodAmount = row.FoodAmount;
        }
        return merged.Values.ToList();
    }

    /// <summary>Movie: ticket lines only (food has no movie). 1 query.</summary>
    private async Task<List<SalesAggregateRow>> AggregateByMovieAsync(IQueryable<Invoice> invoices)
    {
        return await (from t in _db.InvoiceTicket
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
    }

    /// <summary>PaymentMethod: the per-tender rows (what each tender contributed to the invoices). 1 query.</summary>
    private async Task<List<SalesAggregateRow>> AggregateByTenderAsync(IQueryable<Invoice> invoices)
    {
        return await (from p in _db.InvoicePayment
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
