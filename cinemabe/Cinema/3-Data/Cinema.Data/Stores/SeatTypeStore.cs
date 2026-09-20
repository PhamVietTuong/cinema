using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class SeatTypeStore : GenericStore<SeatType>, ISeatTypeStore
{
    public SeatTypeStore(CinemaContext db) : base(db)
    {
    }

    public async Task<IReadOnlyDictionary<SeatKind, Guid>> GetKindMapAsync(Guid theaterId)
        => await DbSet
            .AsNoTracking()
            .Where(s => s.TheaterId == theaterId)
            .Select(s => new { s.Kind, s.Id })
            .ToDictionaryAsync(x => x.Kind, x => x.Id);
}
