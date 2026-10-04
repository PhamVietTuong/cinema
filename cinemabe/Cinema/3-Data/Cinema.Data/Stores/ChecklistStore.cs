using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class ChecklistStore : IChecklistStore
{
    private readonly CinemaContext _db;

    public ChecklistStore(CinemaContext db)
    {
        _db = db;
    }

    public async Task<List<ChecklistTemplate>> GetTemplatesAsync(Guid theaterId)
    {
        var templates = await _db.ChecklistTemplate
            .AsNoTracking()
            .Include(t => t.Items)
            .Where(t => t.TheaterId == theaterId)
            .OrderBy(t => t.Kind).ThenBy(t => t.Name)
            .AsSplitQuery()
            .ToListAsync();
        foreach (var template in templates)
        {
            template.Items = template.Items.OrderBy(i => i.SortOrder).ToList();
        }
        return templates;
    }

    public async Task<ChecklistTemplate?> GetTemplateAsync(Guid id)
    {
        return await _db.ChecklistTemplate.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<ChecklistTemplate?> GetActiveTemplateAsync(Guid theaterId, ChecklistKind kind)
    {
        var template = await _db.ChecklistTemplate
            .AsNoTracking()
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.TheaterId == theaterId && t.Kind == kind && t.IsActive);
        if (template != null)
        {
            template.Items = template.Items.OrderBy(i => i.SortOrder).ToList();
        }
        return template;
    }

    public async Task<bool> HasActiveTemplateAsync(Guid theaterId, ChecklistKind kind, Guid? excludeTemplateId)
    {
        return await _db.ChecklistTemplate.AnyAsync(t => t.TheaterId == theaterId
            && t.Kind == kind
            && t.IsActive
            && (excludeTemplateId == null || t.Id != excludeTemplateId));
    }

    public void StageTemplate(ChecklistTemplate template)
    {
        _db.ChecklistTemplate.Add(template);
    }

    public void StageReplaceTemplateItems(ChecklistTemplate template, IReadOnlyList<ChecklistTemplateItem> items)
    {
        _db.ChecklistTemplateItem.RemoveRange(template.Items.ToList());
        foreach (var item in items)
        {
            item.ChecklistTemplateId = template.Id;
            _db.ChecklistTemplateItem.Add(item);
        }
    }

    public async Task<ChecklistRun?> GetRunAsync(Guid showTimeId, Guid roomId, ChecklistKind kind)
    {
        var run = await _db.ChecklistRun
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.ShowTimeId == showTimeId && r.RoomId == roomId && r.Kind == kind);
        SortItems(run);
        return run;
    }

    public async Task<ChecklistRun?> GetRunByItemAsync(Guid runItemId)
    {
        var run = await _db.ChecklistRun
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Items.Any(i => i.Id == runItemId));
        SortItems(run);
        return run;
    }

    public async Task<ChecklistRun?> GetRunByIdAsync(Guid runId)
    {
        var run = await _db.ChecklistRun.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == runId);
        SortItems(run);
        return run;
    }

    public async Task AddRunAsync(ChecklistRun run)
    {
        _db.ChecklistRun.Add(run);
        await _db.SaveChangesAsync();
    }

    private static void SortItems(ChecklistRun? run)
    {
        if (run != null)
        {
            run.Items = run.Items.OrderBy(i => i.SortOrder).ToList();
        }
    }
}
