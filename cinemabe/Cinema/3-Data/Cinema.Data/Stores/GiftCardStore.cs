using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class GiftCardStore : GenericStore<GiftCard>, IGiftCardStore
{
    public GiftCardStore(CinemaContext db) : base(db)
    {
    }

    public async Task<GiftCard?> GetByCodeAsync(string code)
        => await DbSet.FirstOrDefaultAsync(g => g.Code == code);
}
