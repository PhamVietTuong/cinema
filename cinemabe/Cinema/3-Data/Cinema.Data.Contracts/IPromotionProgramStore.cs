using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IPromotionProgramStore : IGenericStore<PromotionProgram>
{
    /// <summary>A tracked program with its Prices and Schedules loaded (split query, avoids a
    /// cartesian result across the two collections), for Update.</summary>
    Task<PromotionProgram?> GetByIdWithChildrenAsync(Guid id);

    /// <summary>Looks up a program by its promo Code, with Prices and Schedules loaded (split query).</summary>
    Task<PromotionProgram?> GetByCodeAsync(string code);

    /// <summary>Price rows for a page of programs, AsNoTracking, keyed by PromotionProgramId by the caller.</summary>
    Task<List<PromotionProgramPrice>> GetPricesAsync(IReadOnlyCollection<Guid> programIds);

    /// <summary>Schedule rows for a page of programs, AsNoTracking, keyed by PromotionProgramId by the caller.</summary>
    Task<List<PromotionProgramSchedule>> GetSchedulesAsync(IReadOnlyCollection<Guid> programIds);

    /// <summary>Published, auto-apply, price-affecting programs whose date window contains
    /// <paramref name="showDate"/>, with Prices and Schedules loaded (split query). The caller
    /// (BookingManager) still applies eligibility, channel, holiday and schedule matching.</summary>
    Task<List<PromotionProgram>> GetAutoApplyCandidatesAsync(DateOnly showDate);

    /// <summary>Explicitly marks these tracked rows Deleted (no SaveChanges) — the manager calls this
    /// before replacing a program's Prices with a fresh list on Update, rather than relying on
    /// collection.Clear() + Add() in place, which produced a spurious UPDATE/DELETE pairing instead of
    /// a clean DELETE+INSERT (see PromotionProgramManager.ApplyMechanicAndEligibility).</summary>
    void RemovePrices(IEnumerable<PromotionProgramPrice> prices);

    /// <summary>Same as <see cref="RemovePrices"/>, for Schedules.</summary>
    void RemoveSchedules(IEnumerable<PromotionProgramSchedule> schedules);

    /// <summary>Explicitly marks a brand-new row Added (no SaveChanges) — see
    /// PromotionProgramManager.ApplyMechanicAndEligibility for why this can't just be
    /// entity.Prices.Add(...).</summary>
    void AddPrice(PromotionProgramPrice price);

    /// <summary>Same as <see cref="AddPrice"/>, for Schedules.</summary>
    void AddSchedule(PromotionProgramSchedule schedule);
}
