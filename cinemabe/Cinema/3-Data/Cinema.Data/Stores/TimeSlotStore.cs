using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class TimeSlotStore : GenericStore<TimeSlot>, ITimeSlotStore
{
    public TimeSlotStore(CinemaContext db) : base(db)
    {
    }

    public async Task<bool> HasTimeSlotOverlapAsync(Guid theaterId, string startTime, string endTime, Guid? excludeTimeSlotId)
    {
        // Two intervals overlap iff each starts before the other ends. Comparing the "HH:mm" strings
        // directly (rather than parsing) works because, for zero-padded HH:mm values, alphabetical
        // order matches time-of-day order.
        return await DbSet.AnyAsync(s =>
            s.TheaterId == theaterId &&
            (excludeTimeSlotId == null || s.Id != excludeTimeSlotId) &&
            string.Compare(s.StartTime, endTime) < 0 && string.Compare(startTime, s.EndTime) < 0);
    }
}
