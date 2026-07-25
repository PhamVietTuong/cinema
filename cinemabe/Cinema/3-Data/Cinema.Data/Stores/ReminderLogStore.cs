using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class ReminderLogStore : GenericStore<ReminderLog>, IReminderLogStore
{
    public ReminderLogStore(CinemaContext db) : base(db)
    {
    }

    public async Task<bool> WasSentAsync(Guid userId, Guid showTimeId)
        => await ExistsAsync(r => r.UserId == userId && r.ShowTimeId == showTimeId);
}
