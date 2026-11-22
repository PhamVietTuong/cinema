using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class MemberShipStore : GenericStore<MemberShip>, IMemberShipStore
{
    public MemberShipStore(CinemaContext db) : base(db)
    {
    }

    public async Task<MemberShip?> GetHighestTierForPointsAsync(int points)
        => await DbSet.AsNoTracking()
            .Where(m => m.MinPoints <= points)
            .OrderByDescending(m => m.MinPoints)
            .FirstOrDefaultAsync();
}
