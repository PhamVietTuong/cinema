using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class SplashBannerStore : GenericStore<SplashBanner>, ISplashBannerStore
{
    public SplashBannerStore(CinemaContext db) : base(db)
    {
    }

    public Task<SplashBanner?> GetActiveAsync(DateTime today)
    {
        // StartDate/EndDate are always saved date-only (SplashBannerManager truncates to .Date), so
        // comparing the raw columns here (instead of wrapping them in .Date) lets the
        // (IsPublished, EndDate) index actually get used.
        var date = today.Date;
        return DbSet.AsNoTracking()
            .Where(b => b.IsPublished && b.StartDate <= date && date <= b.EndDate)
            .OrderByDescending(b => b.StartDate)
            .ThenByDescending(b => b.CreationTime)
            .ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync();
    }
}
