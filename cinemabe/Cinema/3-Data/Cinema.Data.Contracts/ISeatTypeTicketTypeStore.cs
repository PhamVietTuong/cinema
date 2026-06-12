using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

/// <summary>Composite-key store for the SeatType ↔ TicketType price matrix.</summary>
public interface ISeatTypeTicketTypeStore
{
    Task<IEnumerable<SeatTypeTicketType>> GetAllAsync();
    Task<SeatTypeTicketType?> GetAsync(Guid seatTypeId, Guid ticketTypeId);
    Task AddAsync(SeatTypeTicketType entity);
    Task UpdateAsync(SeatTypeTicketType entity);
    Task DeleteAsync(Guid seatTypeId, Guid ticketTypeId);
}
