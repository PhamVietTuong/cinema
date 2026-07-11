using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class TimeSlotStore : GenericStore<TimeSlot>, ITimeSlotStore
{
    public TimeSlotStore(CinemaContext db) : base(db)
    {
    }
}
