using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class DiscountStore : GenericStore<Discount>, IDiscountStore
{
    public DiscountStore(CinemaContext db) : base(db)
    {
    }
}
