using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class TheaterStore : GenericStore<Theater>, ITheaterStore
{
    public TheaterStore(CinemaContext db) : base(db) { }

    public async Task<(IReadOnlyList<(Theater Theater, int RoomCount)> Items, int Total)> SearchAsync(
        string? name, string? city, int page, int pageSize)
    {
        var query = DbSet.AsNoTracking().Where(t => t.IsActive);

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(t => t.Name.Contains(name));
        }
        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(t => t.City.Contains(city));
        }

        var total = await query.CountAsync();

        var ordered = query.OrderBy(t => t.Name).ThenBy(t => t.Id);
        IQueryable<Theater> paged = ordered;
        if (pageSize > 0)
        {
            paged = ordered.Skip((page - 1) * pageSize).Take(pageSize);
        }

        // Projected so only a Room COUNT subquery runs — Room rows themselves are never loaded.
        var rows = await paged
            .Select(t => new { Theater = t, RoomCount = t.Rooms.Count })
            .ToListAsync();

        return (rows.Select(r => (r.Theater, r.RoomCount)).ToList(), total);
    }

    public async Task<Dictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }
        return await DbSet.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name);
    }

    public async Task<Theater?> GetDetailAsync(Guid id)
        => await DbSet
            .Include(t => t.Rooms)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<IEnumerable<Theater>> GetByMovieAsync(Guid movieId, DateTime date)
        => await DbSet
            .Include(t => t.Rooms).ThenInclude(r => r.ShowTimeRooms).ThenInclude(sr => sr.ShowTime)
            .Where(t => t.IsActive && t.Rooms.Any(r =>
                r.ShowTimeRooms.Any(sr =>
                    sr.ShowTime.MovieId == movieId &&
                    sr.ShowTime.StartTime.Date == date.Date &&
                    sr.ShowTime.IsActive)))
            .ToListAsync();
}
