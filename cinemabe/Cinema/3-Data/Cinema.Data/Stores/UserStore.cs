using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class UserStore : GenericStore<User>, IUserStore
{
    public UserStore(CinemaContext db) : base(db) { }

    public override async Task<User?> GetByIdAsync(Guid id)
        => await DbSet
            .Include(u => u.UserType)
            .Include(u => u.MemberShip)
            .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<bool> TryDeductPointsAsync(Guid userId, int points)
    {
        var rows = await Context.User
            .Where(u => u.Id == userId && u.Points >= points)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Points, u => u.Points - points));
        return rows == 1;
    }

    public async Task<User?> GetByEmailAsync(string email)
        => await DbSet
            .Include(u => u.UserType)
            .Include(u => u.MemberShip)
            .FirstOrDefaultAsync(u => u.Email == email);

    public async Task<User?> GetByPhoneAsync(string phone)
        => await DbSet
            .Include(u => u.UserType)
            .Include(u => u.MemberShip)
            .FirstOrDefaultAsync(u => u.Phone == phone);

    public async Task<(IEnumerable<User> Items, int Total)> GetPagedAsync(
        string? search, int page, int pageSize, Guid? theaterId = null)
    {
        var q = DbSet.Include(u => u.UserType).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(u => u.Name.Contains(search) || u.Email.Contains(search) || u.Phone.Contains(search));
        }
        if (theaterId.HasValue)
        {
            q = q.Where(u => u.TheaterId == theaterId.Value);
        }
        var total = await q.CountAsync();
        var items = await q.OrderBy(u => u.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<Dictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }
        return await DbSet.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);
    }
}
