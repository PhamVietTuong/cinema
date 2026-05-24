using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface ISeatStore : IGenericStore<Seat>
{
    Task<IEnumerable<Seat>> GetByRoomAsync(Guid roomId);
    Task<IEnumerable<Guid>> GetBookedSeatIdsAsync(Guid showTimeId, Guid roomId);
}
