using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class AuditLogStore : GenericStore<AuditLog>, IAuditLogStore
{
    public AuditLogStore(CinemaContext db) : base(db)
    {
    }

    public void Stage(AuditLog entry)
    {
        DbSet.Add(entry);
    }

    public async Task<(List<AuditLog> Items, int Total)> SearchAsync(AuditLogSearchCriteria criteria)
    {
        var query = DbSet.AsNoTracking();
        if (criteria.TheaterIds != null)
        {
            var theaterIds = criteria.TheaterIds;
            query = query.Where(a => a.TheaterId != null && theaterIds.Contains(a.TheaterId.Value));
        }
        if (criteria.Action.HasValue)
        {
            var action = criteria.Action.Value;
            query = query.Where(a => a.Action == action);
        }
        if (criteria.From.HasValue)
        {
            var from = criteria.From.Value;
            query = query.Where(a => a.CreationTime >= from);
        }
        if (criteria.To.HasValue)
        {
            var to = criteria.To.Value;
            query = query.Where(a => a.CreationTime <= to);
        }
        if (criteria.ActorUserId.HasValue)
        {
            var actorId = criteria.ActorUserId.Value;
            query = query.Where(a => a.ActorUserId == actorId);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreationTime)
            .ThenByDescending(a => a.Id)
            .Skip(criteria.PageIndex * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync();
        return (items, total);
    }
}
