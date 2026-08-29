using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class InvoiceStore : GenericStore<Invoice>, IInvoiceStore
{
    public InvoiceStore(CinemaContext db) : base(db) { }

    // Translate a seat-uniqueness violation (another booking, possibly on another instance, took a seat
    // first) into a friendly domain exception instead of a raw persistence error.
    public override async Task<Invoice> CreateAsync(Invoice entity)
    {
        try
        {
            return await base.CreateAsync(entity);
        }
        catch (DbUpdateException ex) when (ex.Entries.Any(e => e.Entity is InvoiceTicket))
        {
            throw new SeatUnavailableException("One or more selected seats are no longer available.", ex);
        }
    }

    public async Task<Invoice?> GetWithDetailsAsync(Guid id)
        => await DbSet
            .Include(i => i.User)
            .Include(i => i.Discount)
            .Include(i => i.InvoiceTickets)
                .ThenInclude(it => it.ShowTimeRoom).ThenInclude(sr => sr.ShowTime).ThenInclude(s => s.Movie)
            .Include(i => i.InvoiceTickets)
                .ThenInclude(it => it.ShowTimeRoom).ThenInclude(sr => sr.Room).ThenInclude(r => r.Theater)
            .Include(i => i.InvoiceTickets).ThenInclude(it => it.Seat).ThenInclude(s => s.SeatType)
            .Include(i => i.InvoiceFoodAndDrinks).ThenInclude(f => f.FoodAndDrink)
            // Tickets and food are independent collections; in one query their rows multiply
            // (6 seats x 4 snacks = 24 rows for a 10-row invoice).
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id);

    public async Task<Invoice?> GetByCodeAsync(string code)
        => await DbSet.FirstOrDefaultAsync(i => i.Code == code);

    public async Task<(IEnumerable<Invoice> Items, int Total)> GetByUserAsync(
        Guid userId, int page, int pageSize)
    {
        var q = DbSet
            .Include(i => i.InvoiceTickets).ThenInclude(it => it.ShowTimeRoom).ThenInclude(sr => sr.ShowTime).ThenInclude(s => s.Movie)
            .Include(i => i.InvoiceTickets).ThenInclude(it => it.Seat)
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreationTime);
        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<(IEnumerable<Invoice> Items, int Total)> GetPagedAsync(
        InvoiceStatus? status, DateTime? from, DateTime? to, int page, int pageSize)
    {
        var q = DbSet.Include(i => i.User).AsQueryable();
        if (status.HasValue)
        {
            q = q.Where(i => i.Status == status.Value);
        }
        if (from.HasValue)
        {
            q = q.Where(i => i.CreationTime >= from.Value);
        }
        if (to.HasValue)
        {
            q = q.Where(i => i.CreationTime <= to.Value);
        }
        q = q.OrderByDescending(i => i.CreationTime);
        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<double> GetTotalRevenueAsync(DateTime from, DateTime to)
        => await DbSet
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidAt >= from && i.PaidAt <= to)
            .SumAsync(i => i.FinalAmount);

    public async Task<IReadOnlyDictionary<DateTime, double>> GetRevenueByDayAsync(DateTime from, DateTime to)
    {
        var rows = await DbSet
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidAt != null && i.PaidAt >= from && i.PaidAt <= to)
            .GroupBy(i => i.PaidAt!.Value.Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(x => x.FinalAmount) })
            .ToListAsync();
        return rows.ToDictionary(r => r.Date, r => r.Total);
    }

    public async Task<IReadOnlyList<InvoiceTicket>> GetPaidTicketsForShowtimesAsync(DateTime from, DateTime to)
        => await Context.InvoiceTicket
            .Include(t => t.Invoice).ThenInclude(i => i.User)
            .Include(t => t.ShowTimeRoom).ThenInclude(sr => sr.ShowTime).ThenInclude(s => s.Movie)
            .Include(t => t.Seat)
            .Where(t => t.Invoice.Status == InvoiceStatus.Paid
                        && t.ShowTimeRoom.ShowTime.StartTime >= from
                        && t.ShowTimeRoom.ShowTime.StartTime < to)
            .ToListAsync();

    public async Task<IReadOnlyDictionary<string, double>> GetRevenueByMovieAsync(DateTime from, DateTime to)
    {
        var rows = await Context.InvoiceTicket
            .Where(t => t.Invoice.Status == InvoiceStatus.Paid && t.Invoice.PaidAt != null
                        && t.Invoice.PaidAt >= from && t.Invoice.PaidAt <= to)
            .GroupBy(t => t.ShowTimeRoom.ShowTime.Movie.Title)
            .Select(g => new { Name = g.Key, Total = g.Sum(x => x.Price) })
            .ToListAsync();
        return rows.ToDictionary(r => r.Name, r => r.Total);
    }

    public async Task<IReadOnlyDictionary<string, double>> GetRevenueByTheaterAsync(DateTime from, DateTime to)
    {
        var rows = await Context.InvoiceTicket
            .Where(t => t.Invoice.Status == InvoiceStatus.Paid && t.Invoice.PaidAt != null
                        && t.Invoice.PaidAt >= from && t.Invoice.PaidAt <= to)
            .GroupBy(t => t.ShowTimeRoom.Room.Theater.Name)
            .Select(g => new { Name = g.Key, Total = g.Sum(x => x.Price) })
            .ToListAsync();
        return rows.ToDictionary(r => r.Name, r => r.Total);
    }

    public async Task<IReadOnlyList<Invoice>> GetStalePendingAsync(DateTime olderThan)
        => await DbSet
            .Where(i => i.Status == InvoiceStatus.Pending && i.CreationTime < olderThan)
            .ToListAsync();

    public async Task<InvoiceTicket?> GetTicketByQrAsync(string qrCode)
        => await Context.InvoiceTicket
            .Include(t => t.Invoice)
            .Include(t => t.Seat)
            .Include(t => t.ShowTimeRoom).ThenInclude(sr => sr.ShowTime).ThenInclude(s => s.Movie)
            .Include(t => t.ShowTimeRoom).ThenInclude(sr => sr.Room)
            .FirstOrDefaultAsync(t => t.QrCode == qrCode);

    public async Task DeactivateTicketsAsync(Guid invoiceId)
        => await Context.InvoiceTicket
            .Where(t => t.InvoiceId == invoiceId && t.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false));
}
