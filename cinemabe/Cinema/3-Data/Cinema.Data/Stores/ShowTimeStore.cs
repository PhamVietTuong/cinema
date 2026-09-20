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
            .Include(s => s.ShowTimeRooms).ThenInclude(sr => sr.Room).ThenInclude(r => r.RoomType)
            .Where(s => s.MovieId == movieId &&
                        s.IsActive &&
                        DateOnly.FromDateTime(s.StartTime) == date &&
                        s.ShowTimeRooms.Any(sr => sr.Room.TheaterId == theaterId))
            .OrderBy(s => s.StartTime)
            .ToListAsync();

    public async Task<IReadOnlyList<MovieScheduleRow>> GetMovieScheduleAsync(Guid movieId, DateTime fromInclusive, DateTime toExclusive)
        => await Context.ShowTimeRoom
            .AsNoTracking()
            .Where(sr => sr.ShowTime.MovieId == movieId &&
                         sr.ShowTime.IsActive &&
                         sr.ShowTime.StartTime >= fromInclusive &&
                         sr.ShowTime.StartTime < toExclusive)
            .OrderBy(sr => sr.ShowTime.StartTime)
            .Select(sr => new MovieScheduleRow(
                sr.ShowTimeId,
                sr.ShowTime.StartTime,
                sr.ShowTime.EndTime,
                sr.ShowTime.ProjectionForm,
                sr.RoomId,
                sr.Room.Name,
                sr.Room.RoomType.Name,
                sr.Room.TheaterId,
                sr.Room.Theater.Name,
                sr.Room.Theater.Address,
                sr.Room.TotalRows * sr.Room.TotalColumns))
            .ToListAsync();

    public async Task<ShowTimeRoom?> GetShowTimeRoomAsync(Guid showTimeId, Guid roomId)
        => await Context.ShowTimeRoom
            .Include(sr => sr.ShowTime).ThenInclude(s => s.Movie)
            .Include(sr => sr.Room).ThenInclude(r => r.Theater)
            .Include(sr => sr.Room).ThenInclude(r => r.RoomType)
            .FirstOrDefaultAsync(sr => sr.ShowTimeId == showTimeId && sr.RoomId == roomId);

    public async Task<(IReadOnlyList<ShowTime> Items, int Total)> SearchAsync(
        Guid? movieId, Guid? roomId, bool? isActive, DateTime? from, DateTime? to, int page, int pageSize)
    {
        var query = DbSet
            .Include(s => s.ShowTimeRooms).ThenInclude(sr => sr.Room).ThenInclude(r => r.RoomType)
            .AsQueryable();

        if (movieId.HasValue) { query = query.Where(s => s.MovieId == movieId.Value); }
        if (roomId.HasValue) { query = query.Where(s => s.ShowTimeRooms.Any(sr => sr.RoomId == roomId.Value)); }
        if (isActive.HasValue) { query = query.Where(s => s.IsActive == isActive.Value); }
        if (from.HasValue) { query = query.Where(s => s.StartTime >= from.Value); }
        if (to.HasValue) { query = query.Where(s => s.StartTime < to.Value); }

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
            .Include(s => s.ShowTimeRooms).ThenInclude(sr => sr.Room).ThenInclude(r => r.RoomType)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<bool> HasRoomOverlapAsync(Guid roomId, DateTime startTime, DateTime endTime, int bufferMinutes, Guid? excludeShowTimeId)
    {
        // Expand the candidate window on both sides by the buffer before comparing, so the room stays
        // exclusively reserved for `bufferMinutes` between the end of one showtime and the start of the
        // next. Computed here, not inside the predicate, so EF doesn't translate AddMinutes per row.
        var windowStart = startTime.AddMinutes(-bufferMinutes);
        var windowEnd = endTime.AddMinutes(bufferMinutes);

        // Two intervals overlap iff each starts before the other ends.
        return await DbSet.AnyAsync(s =>
            s.IsActive &&
            (excludeShowTimeId == null || s.Id != excludeShowTimeId) &&
            s.ShowTimeRooms.Any(sr => sr.RoomId == roomId) &&
            s.StartTime < windowEnd && windowStart < s.EndTime);
    }
}
