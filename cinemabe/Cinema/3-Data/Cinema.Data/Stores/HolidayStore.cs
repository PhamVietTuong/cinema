using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class HolidayStore : GenericStore<Holiday>, IHolidayStore
{
    public HolidayStore(CinemaContext db) : base(db)
    {
    }
}
