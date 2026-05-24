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
}
