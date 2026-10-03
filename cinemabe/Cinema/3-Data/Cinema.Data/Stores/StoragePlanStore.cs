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

    public async Task<(List<StoragePlanListRow> Items, int Total)> SearchAsync(StoragePlanSearchCriteria criteria)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (criteria.TheaterId.HasValue)
        {
            var theaterId = criteria.TheaterId.Value;
            query = query.Where(p => p.TheaterId == theaterId);
        }
        if (criteria.Status.HasValue)
        {
            var status = criteria.Status.Value;
            query = query.Where(p => p.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            var keyword = criteria.Keyword.Trim();
            query = query.Where(p => p.Code.Contains(keyword));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreationTime)
            .ThenBy(p => p.Id)
            .Skip(criteria.PageIndex * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(p => new StoragePlanListRow(
                p.Id, p.Code, p.TheaterId, p.Status, p.TargetDate, p.Supplier,
                p.Items.Count, p.Items.Sum(i => i.PlannedQuantity), p.CreatedByUserId, p.CreationTime))
            .ToListAsync();
        return (items, total);
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
