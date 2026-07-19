using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class DiscountStore : GenericStore<Discount>, IDiscountStore
{
    public DiscountStore(CinemaContext db) : base(db)
    {
    }

    public Task<List<Discount>> GetAllWithScopeAsync()
        => DbSet.Include(d => d.DiscountTheaters)
                .OrderByDescending(d => d.CreationTime)
                .ToListAsync();

    public Task<Discount?> GetByIdWithScopeAsync(Guid id)
        => DbSet.Include(d => d.DiscountTheaters)
                .FirstOrDefaultAsync(d => d.Id == id);

    public Task<Discount?> GetByCodeAsync(string code)
        => DbSet.Include(d => d.DiscountTheaters)
                .FirstOrDefaultAsync(d => d.Code == code);

    public Task<List<Discount>> GetActiveAutoApplyAsync(DateTime now)
        => DbSet.Include(d => d.DiscountTheaters)
                .Where(d => d.AutoApply && d.IsActive && d.StartDate <= now && now <= d.EndDate)
                .ToListAsync();
}
