using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class DiscountTypeStore : GenericStore<DiscountType>, IDiscountTypeStore
{
    public DiscountTypeStore(CinemaContext db) : base(db)
    {
    }
}
