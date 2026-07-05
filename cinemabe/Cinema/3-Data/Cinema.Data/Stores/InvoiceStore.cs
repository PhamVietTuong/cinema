using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class InvoiceStore : GenericStore<Invoice>, IInvoiceStore
{
    public InvoiceStore(CinemaContext db) : base(db) { }

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
        if (status.HasValue) q = q.Where(i => i.Status == status.Value);
        if (from.HasValue) q = q.Where(i => i.CreationTime >= from.Value);
        if (to.HasValue) q = q.Where(i => i.CreationTime <= to.Value);
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

    public async Task<IReadOnlyList<Invoice>> GetStalePendingAsync(DateTime olderThan)
        => await DbSet
            .Where(i => i.Status == InvoiceStatus.Pending && i.CreationTime < olderThan)
            .ToListAsync();
}
