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

    public async Task<IEnumerable<ShowTime>> GetAllWithRoomsAsync()
        => await DbSet
            .Include(s => s.ShowTimeRooms).ThenInclude(sr => sr.Room)
            .OrderBy(s => s.StartTime)
            .ToListAsync();

    public async Task<ShowTime?> GetByIdWithRoomsAsync(Guid id)
        => await DbSet
            .Include(s => s.ShowTimeRooms).ThenInclude(sr => sr.Room)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task SetRoomAsync(Guid showTimeId, Guid roomId, int basePrice)
    {
        var existing = await Context.ShowTimeRoom
            .Where(sr => sr.ShowTimeId == showTimeId)
            .ToListAsync();
        if (existing.Count > 0)
        {
            Context.ShowTimeRoom.RemoveRange(existing);
        }
        await Context.ShowTimeRoom.AddAsync(new ShowTimeRoom
        {
            ShowTimeId = showTimeId,
            RoomId = roomId,
            BasePrice = basePrice,
        });
        await Context.SaveChangesAsync();
    }
}
