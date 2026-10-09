using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class StockMovementStore : GenericStore<StockMovement>, IStockMovementStore
{
    public StockMovementStore(CinemaContext db) : base(db)
    {
    }

    public async Task<List<StockNetQuantity>> GetNetSaleQuantitiesAsync(IReadOnlyCollection<Guid> invoiceIds)
    {
        if (invoiceIds.Count == 0)
        {
            return new List<StockNetQuantity>();
        }
        var rows = await DbSet.AsNoTracking()
            .Where(m => m.InvoiceId != null && invoiceIds.Contains(m.InvoiceId.Value)
                && (m.Type == StockMovementType.Sale || m.Type == StockMovementType.SaleReversal))
            .GroupBy(m => new { InvoiceId = m.InvoiceId!.Value, m.FoodAndDrinkId })
            .Select(g => new { g.Key.InvoiceId, g.Key.FoodAndDrinkId, Net = g.Sum(m => m.Quantity) })
            .ToListAsync();
        return rows.Select(r => new StockNetQuantity(r.InvoiceId, r.FoodAndDrinkId, r.Net)).ToList();
    }

    public async Task<(List<StockMovement> Items, int Total)> SearchAsync(StockMovementSearchCriteria criteria)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (criteria.TheaterId.HasValue)
        {
            var theaterId = criteria.TheaterId.Value;
            query = query.Where(m => m.TheaterId == theaterId);
        }
        if (criteria.FoodAndDrinkId.HasValue)
        {
            var itemId = criteria.FoodAndDrinkId.Value;
            query = query.Where(m => m.FoodAndDrinkId == itemId);
        }
        if (criteria.Type.HasValue)
        {
            var type = criteria.Type.Value;
            query = query.Where(m => m.Type == type);
        }
        if (criteria.From.HasValue)
        {
            var from = criteria.From.Value;
            query = query.Where(m => m.CreationTime >= from);
        }
        if (criteria.To.HasValue)
        {
            var to = criteria.To.Value;
            query = query.Where(m => m.CreationTime <= to);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.CreationTime)
            .ThenByDescending(m => m.Id)
            .Skip(criteria.PageIndex * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync();
        return (items, total);
    }
}
