using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class LoyaltySettingStore : GenericStore<LoyaltySetting>, ILoyaltySettingStore
{
    public LoyaltySettingStore(CinemaContext db) : base(db)
    {
    }

    public async Task<LoyaltySetting?> GetGlobalAsync()
    {
        return await DbSet.AsNoTracking().FirstOrDefaultAsync(s => s.TheaterId == null);
    }

    public async Task<LoyaltySetting?> GetOverrideAsync(Guid theaterId)
    {
        return await DbSet.AsNoTracking().FirstOrDefaultAsync(s => s.TheaterId == theaterId);
    }
}
