using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface ISplashBannerStore : IGenericStore<SplashBanner>
{
    /// <summary>The single published, in-window banner for <paramref name="today"/> (local date,
    /// inclusive on both ends): the one with the latest StartDate, ties broken by CreationTime then
    /// Id, both descending. AsNoTracking — this is a read-only lookup.</summary>
    Task<SplashBanner?> GetActiveAsync(DateTime today);
}
