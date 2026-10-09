using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class FoodAndDrinkStore : GenericStore<FoodAndDrink>, IFoodAndDrinkStore
{
    public FoodAndDrinkStore(CinemaContext db) : base(db)
    {
    }

    public async Task<Dictionary<Guid, FoodAndDrink>> GetByIdsAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, FoodAndDrink>();
        }
        return await DbSet.AsNoTracking().Where(f => ids.Contains(f.Id)).ToDictionaryAsync(f => f.Id);
    }

    public async Task<List<LowStockRow>> GetLowStockAsync(Guid theaterId)
    {
        return await DbSet
            .AsNoTracking()
            .Where(f => f.TheaterId == theaterId && f.TrackInventory && !f.IsCombo && f.QuantityOnHand <= f.LowStockThreshold)
            .OrderBy(f => f.QuantityOnHand).ThenBy(f => f.Name)
            .Select(f => new LowStockRow
            {
                FoodAndDrinkId = f.Id,
                Name = f.Name,
                QuantityOnHand = f.QuantityOnHand,
                LowStockThreshold = f.LowStockThreshold,
                TargetStockLevel = f.TargetStockLevel
            })
            .ToListAsync();
    }

    public async Task<bool> TryApplyStockDeltaAsync(Guid foodAndDrinkId, int delta)
    {
        var now = DateTime.UtcNow;
        var rows = await DbSet
            .Where(f => f.Id == foodAndDrinkId && f.QuantityOnHand + delta >= 0)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.QuantityOnHand, f => f.QuantityOnHand + delta)
                .SetProperty(f => f.LastUpdatedTime, now));
        return rows == 1;
    }

    public async Task<(List<FoodAndDrink> Items, int Total)> SearchInventoryAsync(InventorySearchCriteria criteria)
    {
        var query = DbSet.AsNoTracking().Where(f => !f.IsCombo);
        if (criteria.TheaterId.HasValue)
        {
            var theaterId = criteria.TheaterId.Value;
            query = query.Where(f => f.TheaterId == theaterId);
        }
        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            var keyword = criteria.Keyword.Trim();
            query = query.Where(f => f.Name.Contains(keyword));
        }
        if (criteria.TrackedOnly)
        {
            query = query.Where(f => f.TrackInventory);
        }
        if (criteria.LowStock.HasValue)
        {
            var low = criteria.LowStock.Value;
            query = low
                ? query.Where(f => f.TrackInventory && f.QuantityOnHand <= f.LowStockThreshold)
                : query.Where(f => !(f.TrackInventory && f.QuantityOnHand <= f.LowStockThreshold));
        }
        if (criteria.OutOfStock.HasValue)
        {
            var outOfStock = criteria.OutOfStock.Value;
            query = outOfStock
                ? query.Where(f => f.TrackInventory && f.QuantityOnHand == 0)
                : query.Where(f => !(f.TrackInventory && f.QuantityOnHand == 0));
        }

        var total = await query.CountAsync();

        IOrderedQueryable<FoodAndDrink> ordered;
        if (criteria.SortByQuantity)
        {
            ordered = criteria.Ascending ? query.OrderBy(f => f.QuantityOnHand) : query.OrderByDescending(f => f.QuantityOnHand);
        }
        else
        {
            ordered = criteria.Ascending ? query.OrderBy(f => f.Name) : query.OrderByDescending(f => f.Name);
        }

        var items = await ordered
            .ThenBy(f => f.Id)
            .Skip(criteria.PageIndex * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(f => new FoodAndDrink
            {
                Id = f.Id,
                TheaterId = f.TheaterId,
                Name = f.Name,
                ImageUrl = f.ImageUrl,
                Price = f.Price,
                IsAvailable = f.IsAvailable,
                TrackInventory = f.TrackInventory,
                QuantityOnHand = f.QuantityOnHand,
                LowStockThreshold = f.LowStockThreshold,
                TargetStockLevel = f.TargetStockLevel
            })
            .ToListAsync();
        return (items, total);
    }
}
