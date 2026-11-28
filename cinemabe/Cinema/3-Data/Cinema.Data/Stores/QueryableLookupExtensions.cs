using System.Linq.Expressions;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

internal static class QueryableLookupExtensions
{
    private sealed class IdValue<TValue>
    {
        public Guid Id { get; set; }
        public TValue Value { get; set; } = default!;
    }

    /// <summary>
    /// One read-only query that maps each requested id to a projected value (a name, a title...). Ids with no row
    /// are simply absent from the result. Replaces loading whole tracked entities just to read one column, and
    /// keeps the projection in SQL (only Id + the selected column are fetched).
    /// </summary>
    public static async Task<Dictionary<Guid, TValue>> ProjectByIdsAsync<TEntity, TValue>(
        this IQueryable<TEntity> query,
        IReadOnlyCollection<Guid> ids,
        Expression<Func<TEntity, TValue>> valueSelector)
        where TEntity : BaseEntity
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, TValue>();
        }

        // e => new IdValue<TValue> { Id = e.Id, Value = <valueSelector body over e> }, reusing the selector's own
        // parameter so EF can translate the whole expression.
        var parameter = valueSelector.Parameters[0];
        var projection = Expression.Lambda<Func<TEntity, IdValue<TValue>>>(
            Expression.MemberInit(
                Expression.New(typeof(IdValue<TValue>)),
                Expression.Bind(typeof(IdValue<TValue>).GetProperty(nameof(IdValue<TValue>.Id))!, Expression.Property(parameter, nameof(BaseEntity.Id))),
                Expression.Bind(typeof(IdValue<TValue>).GetProperty(nameof(IdValue<TValue>.Value))!, valueSelector.Body)),
            parameter);

        var rows = await query
            .AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(projection)
            .ToListAsync();
        return rows.ToDictionary(r => r.Id, r => r.Value);
    }
}
