using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class ScheduleBoardStore : IScheduleBoardStore
{
    private readonly CinemaContext _db;

    public ScheduleBoardStore(CinemaContext db)
    {
        _db = db;
    }

    public async Task<List<BoardRoomRow>> GetRoomsAsync(Guid theaterId)
    {
        return await _db.Room
            .AsNoTracking()
            .Where(r => r.TheaterId == theaterId)
            .OrderBy(r => r.Name)
            .Select(r => new BoardRoomRow(
                r.Id,
                r.Name,
                r.RoomType.Name,
                r.Status,
                r.RoomType.TurnoverBufferMinutes,
                r.Seats.Count(s => s.IsActive)))
            .ToListAsync();
    }

    public async Task<List<BoardShowTimeRow>> GetShowTimesAsync(Guid theaterId, DateTime from, DateTime to)
    {
        return await _db.ShowTimeRoom
            .AsNoTracking()
            .Where(x => x.Room.TheaterId == theaterId
                && x.ShowTime.IsActive
                && x.ShowTime.StartTime >= from
                && x.ShowTime.StartTime < to)
            .OrderBy(x => x.ShowTime.StartTime)
            .Select(x => new BoardShowTimeRow(
                x.ShowTimeId,
                x.RoomId,
                x.ShowTime.MovieId,
                x.ShowTime.Movie.Title,
                x.ShowTime.StartTime,
                x.ShowTime.EndTime))
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<(Guid ShowTimeId, Guid RoomId), int>> GetSoldCountsAsync(Guid theaterId, DateTime from, DateTime to)
    {
        var rows = await _db.InvoiceTicket
            .AsNoTracking()
            .Where(t => t.IsActive
                && t.ShowTimeRoom.Room.TheaterId == theaterId
                && t.ShowTimeRoom.ShowTime.StartTime >= from
                && t.ShowTimeRoom.ShowTime.StartTime < to
                && (t.Invoice.Status == InvoiceStatus.Paid || t.Invoice.Status == InvoiceStatus.Pending))
            .GroupBy(t => new { t.ShowTimeId, t.RoomId })
            .Select(g => new { g.Key.ShowTimeId, g.Key.RoomId, Count = g.Count() })
            .ToListAsync();

        return rows.ToDictionary(r => (r.ShowTimeId, r.RoomId), r => r.Count);
    }
}
