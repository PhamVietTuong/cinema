using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class PromotionProgramStore : GenericStore<PromotionProgram>, IPromotionProgramStore
{
    public PromotionProgramStore(CinemaContext db) : base(db)
    {
    }

    public Task<PromotionProgram?> GetByIdWithChildrenAsync(Guid id)
        => DbSet.Include(p => p.Prices)
                .Include(p => p.Schedules)
                .AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Id == id);

    public Task<List<PromotionProgramPrice>> GetPricesAsync(IReadOnlyCollection<Guid> programIds)
        => Context.PromotionProgramPrice
                .AsNoTracking()
                .Where(x => programIds.Contains(x.PromotionProgramId))
                .ToListAsync();

    public Task<List<PromotionProgramSchedule>> GetSchedulesAsync(IReadOnlyCollection<Guid> programIds)
        => Context.PromotionProgramSchedule
                .AsNoTracking()
                .Where(x => programIds.Contains(x.PromotionProgramId))
                .ToListAsync();

    public Task<List<PromotionProgram>> GetAutoApplyCandidatesAsync(DateOnly showDate)
    {
        var date = showDate.ToDateTime(TimeOnly.MinValue);
        return DbSet.AsNoTracking()
                .Include(p => p.Prices)
                .Include(p => p.Schedules)
                .AsSplitQuery()
                .Where(p => p.IsPublished
                            && p.AutoApply
                            && p.Mechanic != PromotionMechanic.Info
                            && (p.Eligibility == PromotionEligibility.Everyone || p.Eligibility == PromotionEligibility.Members)
                            && p.StartDate.Date <= date && date <= p.EndDate.Date)
                .ToListAsync();
    }
}
