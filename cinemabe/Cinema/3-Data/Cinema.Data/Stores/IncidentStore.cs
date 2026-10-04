using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class IncidentStore : GenericStore<Incident>, IIncidentStore
{
    public IncidentStore(CinemaContext db) : base(db)
    {
    }

    public async Task<(List<IncidentRow> Items, int Total)> SearchAsync(IncidentSearchCriteria criteria)
    {
        var query = DbSet.AsNoTracking();
        if (criteria.TheaterIds != null)
        {
            var theaterIds = criteria.TheaterIds;
            query = query.Where(i => theaterIds.Contains(i.TheaterId));
        }
        if (criteria.Status.HasValue)
        {
            var status = criteria.Status.Value;
            query = query.Where(i => i.Status == status);
        }
        if (criteria.Category.HasValue)
        {
            var category = criteria.Category.Value;
            query = query.Where(i => i.Category == category);
        }
        if (criteria.From.HasValue)
        {
            var from = criteria.From.Value;
            query = query.Where(i => i.CreationTime >= from);
        }
        if (criteria.To.HasValue)
        {
            var to = criteria.To.Value;
            query = query.Where(i => i.CreationTime <= to);
        }

        var total = await query.CountAsync();
        var page = await query
            .OrderByDescending(i => i.CreationTime)
            .ThenByDescending(i => i.Id)
            .Skip(criteria.PageIndex * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync();
        return (await WithLabelsAsync(page), total);
    }

    public async Task<IncidentRow?> GetRowAsync(Guid id)
    {
        var incident = await DbSet.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        if (incident == null)
        {
            return null;
        }
        return (await WithLabelsAsync(new List<Incident> { incident }))[0];
    }

    public async Task<List<AffectedTicketRow>> GetUpcomingTicketsAsync(Guid roomId, IReadOnlyCollection<Guid>? seatIds, DateTime now)
    {
        var query = Context.InvoiceTicket
            .AsNoTracking()
            .Where(t => t.RoomId == roomId
                && t.IsActive
                && !t.IsUsed
                && (t.Invoice.Status == InvoiceStatus.Paid || t.Invoice.Status == InvoiceStatus.Pending)
                && t.ShowTimeRoom.ShowTime.EndTime > now);
        if (seatIds != null)
        {
            query = query.Where(t => seatIds.Contains(t.SeatId));
        }

        return await query
            .OrderBy(t => t.ShowTimeRoom.ShowTime.StartTime)
            .ThenBy(t => t.Seat.RowName)
            .ThenBy(t => t.Seat.ColIndex)
            .Select(t => new AffectedTicketRow(
                t.InvoiceId,
                t.Invoice.Code,
                t.Invoice.User.Name,
                t.Invoice.User.Phone,
                t.ShowTimeId,
                t.RoomId,
                t.ShowTimeRoom.Room.Name,
                t.ShowTimeRoom.ShowTime.Movie.Title,
                t.ShowTimeRoom.ShowTime.StartTime,
                t.SeatId,
                t.Seat.RowName,
                t.Seat.ColIndex))
            .ToListAsync();
    }

    /// <summary>Room names and seat labels for a page of incidents: two batched lookups, never one per row.</summary>
    private async Task<List<IncidentRow>> WithLabelsAsync(List<Incident> incidents)
    {
        var roomIds = incidents.Where(i => i.RoomId.HasValue).Select(i => i.RoomId!.Value).Distinct().ToList();
        var seatIds = incidents.Where(i => i.SeatId.HasValue).Select(i => i.SeatId!.Value).Distinct().ToList();

        var rooms = roomIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await Context.Room.AsNoTracking()
                .Where(r => roomIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Name })
                .ToDictionaryAsync(r => r.Id, r => r.Name);
        var seats = seatIds.Count == 0
            ? new Dictionary<Guid, (string RowName, int ColIndex)>()
            : (await Context.Seat.AsNoTracking()
                .Where(s => seatIds.Contains(s.Id))
                .Select(s => new { s.Id, s.RowName, s.ColIndex })
                .ToListAsync())
                .ToDictionary(s => s.Id, s => (s.RowName, s.ColIndex));

        return incidents.Select(i => new IncidentRow(
            i,
            i.RoomId.HasValue && rooms.TryGetValue(i.RoomId.Value, out var roomName) ? roomName : null,
            i.SeatId.HasValue && seats.TryGetValue(i.SeatId.Value, out var seat) ? seat.RowName : null,
            i.SeatId.HasValue && seats.TryGetValue(i.SeatId.Value, out var seatCol) ? seatCol.ColIndex : null)).ToList();
    }
}
