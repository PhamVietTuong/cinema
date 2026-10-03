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
}
