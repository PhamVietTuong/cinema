using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
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
        string? search, int page, int pageSize)
    {
        var q = DbSet.Include(u => u.UserType).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(u => u.Name.Contains(search) || u.Email.Contains(search) || u.Phone.Contains(search));
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

    public async Task<List<(Guid Id, string Name)>> GetApproversAsync(
        Guid theaterId, IReadOnlyCollection<string> theaterRoleNames, IReadOnlyCollection<string> globalRoleNames)
    {
        var rows = await DbSet.AsNoTracking()
            .Where(u => u.Status == UserStatus.Active
                && ((u.TheaterId == theaterId && theaterRoleNames.Contains(u.UserType.Name))
                    || globalRoleNames.Contains(u.UserType.Name)))
            .OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name })
            .ToListAsync();
        return rows.Select(r => (r.Id, r.Name)).ToList();
    }
}
