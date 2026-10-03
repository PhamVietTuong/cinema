using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class ComboItemStore : GenericStore<ComboItem>, IComboItemStore
{
    public ComboItemStore(CinemaContext db) : base(db)
    {
    }

    public async Task<List<ComboItem>> GetByCombosAsync(IReadOnlyCollection<Guid> comboIds)
    {
        if (comboIds.Count == 0)
        {
            return new List<ComboItem>();
        }
        return await DbSet.AsNoTracking().Where(c => comboIds.Contains(c.ComboId)).ToListAsync();
    }

    public async Task<List<(Guid ComboId, string ComboName)>> GetCombosUsingAsync(Guid componentId)
    {
        var rows = await DbSet.AsNoTracking()
            .Where(c => c.ComponentId == componentId)
            .Select(c => new { c.ComboId, c.Combo.Name })
            .ToListAsync();
        return rows.Select(r => (r.ComboId, r.Name)).ToList();
    }

    public async Task ReplaceForComboAsync(Guid comboId, IReadOnlyList<ComboItem> items)
    {
        await DbSet.Where(c => c.ComboId == comboId).ExecuteDeleteAsync();
        foreach (var item in items)
        {
            item.ComboId = comboId;
        }
        await DbSet.AddRangeAsync(items);
        await Context.SaveChangesAsync();
    }
}
