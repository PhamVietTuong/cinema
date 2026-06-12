using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class SeatTypeTicketTypeStore : ISeatTypeTicketTypeStore
{
    private readonly CinemaContext _db;

    public SeatTypeTicketTypeStore(CinemaContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<SeatTypeTicketType>> GetAllAsync()
    {
        return await _db.Set<SeatTypeTicketType>()
            .Include(x => x.SeatType)
            .Include(x => x.TicketType)
            .ToListAsync();
    }

    public async Task<SeatTypeTicketType?> GetAsync(Guid seatTypeId, Guid ticketTypeId)
    {
        return await _db.Set<SeatTypeTicketType>()
            .FirstOrDefaultAsync(x => x.SeatTypeId == seatTypeId && x.TicketTypeId == ticketTypeId);
    }

    public async Task AddAsync(SeatTypeTicketType entity)
    {
        _db.Set<SeatTypeTicketType>().Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(SeatTypeTicketType entity)
    {
        _db.Set<SeatTypeTicketType>().Update(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid seatTypeId, Guid ticketTypeId)
    {
        var entity = await GetAsync(seatTypeId, ticketTypeId);
        if (entity != null)
        {
            _db.Set<SeatTypeTicketType>().Remove(entity);
            await _db.SaveChangesAsync();
        }
    }
}
