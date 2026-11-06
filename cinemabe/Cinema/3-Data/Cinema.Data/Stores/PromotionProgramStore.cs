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

    public Task<PromotionProgram?> GetByCodeAsync(string code)
        => DbSet.Include(p => p.Prices)
                .Include(p => p.Schedules)
                .AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Code == code);

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

    public void RemovePrices(IEnumerable<PromotionProgramPrice> prices)
        => Context.PromotionProgramPrice.RemoveRange(prices);

    public void RemoveSchedules(IEnumerable<PromotionProgramSchedule> schedules)
        => Context.PromotionProgramSchedule.RemoveRange(schedules);

    // A brand-new child discovered only via collection-navigation fixup (entity.Prices.Add(...)), never
    // through an explicit DbSet.Add(), gets tracked as Modified instead of Added once its Guid Id is a
    // non-default, client-set value (BaseEntity sets Id = Guid.NewGuid() in its property initializer) —
    // EF's heuristic for navigation-discovered entities with an already-set key assumes "this might
    // reference an existing row" rather than "this is new". An explicit Add() here removes that
    // ambiguity; the manager still also puts the row in entity.Prices/Schedules so the in-memory graph
    // MapPageAsync reads afterwards is correct.
    public void AddPrice(PromotionProgramPrice price)
        => Context.PromotionProgramPrice.Add(price);

    public void AddSchedule(PromotionProgramSchedule schedule)
        => Context.PromotionProgramSchedule.Add(schedule);

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
