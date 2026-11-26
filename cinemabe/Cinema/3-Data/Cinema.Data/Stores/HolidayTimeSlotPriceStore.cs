using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class HolidayTimeSlotPriceStore : GenericStore<HolidayTimeSlotPrice>, IHolidayTimeSlotPriceStore
{
    public HolidayTimeSlotPriceStore(CinemaContext db) : base(db)
    {
    }
}
