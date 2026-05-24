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
            q = q.Where(u => u.Name.Contains(search) || u.Email.Contains(search) || u.Phone.Contains(search));
        var total = await q.CountAsync();
        var items = await q.OrderBy(u => u.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }
}
