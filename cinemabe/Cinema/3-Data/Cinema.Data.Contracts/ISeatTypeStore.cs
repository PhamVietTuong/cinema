using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface ISeatTypeStore : IGenericStore<SeatType>
{
    /// <summary>One query mapping each SeatKind to its SeatType.Id for a theater (exactly 2 rows).</summary>
    Task<IReadOnlyDictionary<SeatKind, Guid>> GetKindMapAsync(Guid theaterId);
}
