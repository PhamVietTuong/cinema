using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class TicketTypeStore : GenericStore<TicketType>, ITicketTypeStore
{
    public TicketTypeStore(CinemaContext db) : base(db)
    {
    }
}
