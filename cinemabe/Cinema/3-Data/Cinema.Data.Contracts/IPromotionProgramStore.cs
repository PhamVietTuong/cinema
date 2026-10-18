using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IPromotionProgramStore : IGenericStore<PromotionProgram>
{
    /// <summary>A tracked program with its Prices and Schedules loaded (split query, avoids a
    /// cartesian result across the two collections), for Update.</summary>
    Task<PromotionProgram?> GetByIdWithChildrenAsync(Guid id);

    /// <summary>Price rows for a page of programs, AsNoTracking, keyed by PromotionProgramId by the caller.</summary>
    Task<List<PromotionProgramPrice>> GetPricesAsync(IReadOnlyCollection<Guid> programIds);

    /// <summary>Schedule rows for a page of programs, AsNoTracking, keyed by PromotionProgramId by the caller.</summary>
    Task<List<PromotionProgramSchedule>> GetSchedulesAsync(IReadOnlyCollection<Guid> programIds);

    /// <summary>Published, auto-apply, price-affecting programs whose date window contains
    /// <paramref name="showDate"/>, with Prices and Schedules loaded (split query). The caller
    /// (BookingManager) still applies eligibility, channel, holiday and schedule matching.</summary>
    Task<List<PromotionProgram>> GetAutoApplyCandidatesAsync(DateOnly showDate);
}
