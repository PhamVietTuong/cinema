using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class ShowTimeStore : GenericStore<ShowTime>, IShowTimeStore
{
    public ShowTimeStore(CinemaContext db) : base(db) { }

    public async Task<IEnumerable<ShowTime>> GetByMovieAndDateAsync(
        Guid movieId, Guid theaterId, DateOnly date)
        => await DbSet
            .Include(s => s.ShowTimeRooms).ThenInclude(sr => sr.Room).ThenInclude(r => r.Theater)
            .Where(s => s.MovieId == movieId &&
                        s.IsActive &&
                        DateOnly.FromDateTime(s.StartTime) == date &&
                        s.ShowTimeRooms.Any(sr => sr.Room.TheaterId == theaterId))
            .OrderBy(s => s.StartTime)
            .ToListAsync();

    public async Task<ShowTimeRoom?> GetShowTimeRoomAsync(Guid showTimeId, Guid roomId)
        => await Context.ShowTimeRoom
            .Include(sr => sr.ShowTime).ThenInclude(s => s.Movie)
            .Include(sr => sr.Room).ThenInclude(r => r.Theater)
            .FirstOrDefaultAsync(sr => sr.ShowTimeId == showTimeId && sr.RoomId == roomId);

    public async Task<(IReadOnlyList<ShowTime> Items, int Total)> SearchAsync(
        Guid? movieId, Guid? roomId, bool? isActive, int page, int pageSize)
    {
        var query = DbSet
            .Include(s => s.ShowTimeRooms).ThenInclude(sr => sr.Room)
            .AsQueryable();

        if (movieId.HasValue) { query = query.Where(s => s.MovieId == movieId.Value); }
        if (roomId.HasValue) { query = query.Where(s => s.ShowTimeRooms.Any(sr => sr.RoomId == roomId.Value)); }
        if (isActive.HasValue) { query = query.Where(s => s.IsActive == isActive.Value); }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(s => s.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<ShowTime?> GetByIdWithRoomsAsync(Guid id)
        => await DbSet
            .Include(s => s.ShowTimeRooms).ThenInclude(sr => sr.Room)
            .FirstOrDefaultAsync(s => s.Id == id);
}
