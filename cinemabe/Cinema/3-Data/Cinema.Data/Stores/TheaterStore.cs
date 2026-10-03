using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class TheaterStore : GenericStore<Theater>, ITheaterStore
{
    public TheaterStore(CinemaContext db) : base(db) { }

    public async Task<IEnumerable<Theater>> GetTheatersWithRoomsAsync()
        => await DbSet.Include(t => t.Rooms).Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();

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
