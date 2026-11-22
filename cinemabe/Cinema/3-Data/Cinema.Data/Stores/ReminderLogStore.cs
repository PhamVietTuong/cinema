using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class ReminderLogStore : GenericStore<ReminderLog>, IReminderLogStore
{
    public ReminderLogStore(CinemaContext db) : base(db)
    {
    }

    public async Task<bool> WasSentAsync(Guid userId, Guid showTimeId)
        => await ExistsAsync(r => r.UserId == userId && r.ShowTimeId == showTimeId);

    public async Task<HashSet<(Guid UserId, Guid ShowTimeId)>> GetSentAmongAsync(IReadOnlyCollection<Guid> showTimeIds)
    {
        if (showTimeIds.Count == 0)
        {
            return new HashSet<(Guid UserId, Guid ShowTimeId)>();
        }
        var rows = await Context.Set<ReminderLog>().AsNoTracking()
            .Where(r => showTimeIds.Contains(r.ShowTimeId))
            .Select(r => new { r.UserId, r.ShowTimeId })
            .ToListAsync();
        return rows.Select(r => (r.UserId, r.ShowTimeId)).ToHashSet();
    }
}
