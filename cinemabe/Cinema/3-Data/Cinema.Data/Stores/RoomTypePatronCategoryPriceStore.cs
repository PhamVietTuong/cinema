using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class RoomTypePatronCategoryPriceStore : GenericStore<RoomTypePatronCategoryPrice>, IRoomTypePatronCategoryPriceStore
{
    public RoomTypePatronCategoryPriceStore(CinemaContext db) : base(db) { }

    public async Task<IReadOnlyList<RoomTypePatronCategoryPrice>> FindByPatronCategoriesAsync(IReadOnlyCollection<Guid> patronCategoryIds)
        => await DbSet
            .AsNoTracking()
            .Where(x => patronCategoryIds.Contains(x.PatronCategoryId))
            .ToListAsync();

    public async Task<IReadOnlyList<RoomTypePatronCategoryPrice>> FindByRoomTypeAsync(Guid roomTypeId)
        => await DbSet
            .AsNoTracking()
            .Where(x => x.RoomTypeId == roomTypeId)
            .ToListAsync();

    public async Task DeleteByRoomTypeAsync(Guid roomTypeId)
    {
        var existing = await DbSet.Where(x => x.RoomTypeId == roomTypeId).ToListAsync();
        if (existing.Count > 0)
        {
            DbSet.RemoveRange(existing);
            await Context.SaveChangesAsync();
        }
    }
}
