using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class StoragePlanStore : GenericStore<StoragePlan>, IStoragePlanStore
{
    public StoragePlanStore(CinemaContext db) : base(db)
    {
    }

    public async Task<StoragePlan?> GetWithItemsAsync(Guid id)
        => await DbSet.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id);

    public override async Task<StoragePlan> UpdateAsync(StoragePlan entity)
    {
        try
        {
            return await base.UpdateAsync(entity);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The storage plan was modified by someone else.", ex);
        }
    }

    public async Task<Dictionary<Guid, string>> GetCodesByIdsAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }
        return await DbSet.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code);
    }
}
