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

    public async Task<bool> TryDrawBalanceAsync(Guid cardId, double amount)
    {
        var rows = await Context.GiftCard
            .Where(g => g.Id == cardId && g.Balance >= amount)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.Balance, g => g.Balance - amount));
        return rows == 1;
    }

    public async Task<GiftCard?> GetByCodeAsync(string code)
        => await DbSet.FirstOrDefaultAsync(g => g.Code == code);
}
