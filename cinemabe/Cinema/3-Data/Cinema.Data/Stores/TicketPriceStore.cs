using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class TicketPriceStore : GenericStore<TicketPrice>, ITicketPriceStore
{
    public TicketPriceStore(CinemaContext db) : base(db)
    {
    }
}
