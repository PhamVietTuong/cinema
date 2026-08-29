using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class SeatStore : GenericStore<Seat>, ISeatStore
{
    public SeatStore(CinemaContext db) : base(db) { }

    public async Task<IEnumerable<Seat>> GetByRoomAsync(Guid roomId)
        => await DbSet
            .Include(s => s.SeatType)
            .Where(s => s.RoomId == roomId && s.IsActive)
            .OrderBy(s => s.RowName).ThenBy(s => s.ColIndex)
            .ToListAsync();

    public async Task<IEnumerable<Guid>> GetBookedSeatIdsAsync(Guid showTimeId, Guid roomId)
        => await Context.InvoiceTicket
            .Where(it => it.ShowTimeId == showTimeId &&
                         it.RoomId == roomId &&
                         (it.Invoice.Status == InvoiceStatus.Paid || it.Invoice.Status == InvoiceStatus.Pending))
            .Select(it => it.SeatId)
            .Distinct()
            .ToListAsync();

    public async Task<IReadOnlyDictionary<(Guid ShowTimeId, Guid RoomId), int>> GetBookedSeatCountsByMovieAsync(Guid movieId)
    {
        var rows = await Context.InvoiceTicket
            .Where(it => it.ShowTimeRoom.ShowTime.MovieId == movieId &&
                         (it.Invoice.Status == InvoiceStatus.Paid || it.Invoice.Status == InvoiceStatus.Pending))
            .Select(it => new { it.ShowTimeId, it.RoomId, it.SeatId })
            .Distinct()
            .GroupBy(x => new { x.ShowTimeId, x.RoomId })
            .Select(g => new { g.Key.ShowTimeId, g.Key.RoomId, Count = g.Count() })
            .ToListAsync();

        return rows.ToDictionary(r => (r.ShowTimeId, r.RoomId), r => r.Count);
    }
}
