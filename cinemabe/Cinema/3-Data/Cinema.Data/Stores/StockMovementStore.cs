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
}
