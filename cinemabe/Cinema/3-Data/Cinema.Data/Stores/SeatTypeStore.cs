using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class SeatTypeStore : GenericStore<SeatType>, ISeatTypeStore
{
    public SeatTypeStore(CinemaContext db) : base(db)
    {
    }
}
