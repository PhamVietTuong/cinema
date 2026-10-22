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
            .Where(s => s.RoomId == roomId && s.IsActive && s.Kind != SeatKind.Aisle)
            .OrderBy(s => s.RowName).ThenBy(s => s.ColIndex)
            .ToListAsync();

    public async Task<List<Seat>> GetSeatMapByRoomNoTrackingAsync(Guid roomId)
        => await DbSet
            .AsNoTracking()
            .Where(s => s.RoomId == roomId)
            .OrderBy(s => s.RowName).ThenBy(s => s.ColIndex)
            .ToListAsync();

    public async Task<IReadOnlyDictionary<Guid, Seat>> GetByIdsAsync(IReadOnlyCollection<Guid> ids)
        => await DbSet
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);

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

    public async Task<IReadOnlyDictionary<Guid, int>> GetBookableSeatCountsAsync(IReadOnlyCollection<Guid> roomIds)
    {
        var rows = await DbSet
            .Where(s => roomIds.Contains(s.RoomId) && s.IsActive && s.Kind != SeatKind.Aisle)
            .GroupBy(s => s.RoomId)
            .Select(g => new { RoomId = g.Key, Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(r => r.RoomId, r => r.Count);
    }

    public async Task<bool> HasAnyTicketAsync(Guid seatId)
        => await Context.InvoiceTicket.AnyAsync(it => it.SeatId == seatId);

    public async Task<int> ShiftRowLeftAsync(Guid roomId, string rowName, int afterColIndex)
        => await DbSet
            .Where(s => s.RoomId == roomId && s.RowName == rowName && s.ColIndex > afterColIndex)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.ColIndex, s => s.ColIndex - 1));
}
