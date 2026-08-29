using System.Linq.Expressions;
using System.Reflection;
using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Foundation.Logging;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class GenericStore<Entity> : IGenericStore<Entity> where Entity : BaseEntity
{
    protected readonly CinemaContext Context;
    protected readonly DbSet<Entity> DbSet;

    public GenericStore(CinemaContext db)
    {
        Context = db;
        DbSet   = db.Set<Entity>();
    }

    #region FilterQuery

    public virtual IQueryable<Entity> GetQuery()
    {
        return DbSet.AsQueryable();
    }

    public IQueryable<Entity> GetQuery(string linkedElements)
    {
        IQueryable<Entity> query = DbSet.AsQueryable();
        foreach (var element in linkedElements.Split(','))
            query = query.Include(element);
        return query;
    }

    public IQueryable<Entity> FilterQuery(IQueryable<Entity> query, Expression<Func<Entity, bool>> whereExpression)
        => query.Where(whereExpression);

    public IQueryable<Entity> FilterQuery(IQueryable<Entity> query, string navigationPath, Expression<Func<Entity, bool>> whereExpression)
        => query.Include(navigationPath).Where(whereExpression);

    public IOrderedQueryable<Entity> OrderQuery<T>(IQueryable<Entity> query, Expression<Func<Entity, T>> keySelector, bool isAscending = true)
        => isAscending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector);

    public IOrderedQueryable<Entity> OrderQuery<T>(IOrderedQueryable<Entity> query, Expression<Func<Entity, T>> keySelector, bool isAscending = true)
        => isAscending ? query.ThenBy(keySelector) : query.ThenByDescending(keySelector);

    #endregion

    #region CRUD (IGenericStore)

    public virtual async Task<Entity?> GetByIdAsync(Guid id)
    {
        try
        {
            return await DbSet.FindAsync(new object[] { id });
        }
        catch (Exception ex)
        {
            LogProvider.Current.Error(ex, $"GenericStore.GetByIdAsync failed: {ex.Message}");
            throw;
        }
    }

    public virtual async Task<IEnumerable<Entity>> GetAllAsync()
    {
        try
        {
            return await DbSet.ToListAsync();
        }
        catch (Exception ex)
        {
            LogProvider.Current.Error(ex, $"GenericStore.GetAllAsync failed: {ex.Message}");
            throw;
        }
    }

    public virtual async Task<IEnumerable<Entity>> FindAsync(Expression<Func<Entity, bool>> predicate)
    {
        try
        {
            return await DbSet.Where(predicate).ToListAsync();
        }
        catch (Exception ex)
        {
            LogProvider.Current.Error(ex, $"GenericStore.FindAsync failed: {ex.Message}");
            throw;
        }
    }

    public virtual async Task<Entity> CreateAsync(Entity entity)
    {
        try
        {
            var idProperty = typeof(Entity).GetProperty("Id");
            if (idProperty != null && idProperty.PropertyType != typeof(int))
            {
                var currentId = (Guid?)idProperty.GetValue(entity);
                if (currentId == null || currentId == Guid.Empty)
                    idProperty.SetValue(entity, Guid.NewGuid());
            }

            var creationTimeProperty = typeof(Entity).GetProperty("CreationTime");
            if (creationTimeProperty != null)
            {
                var current = creationTimeProperty.GetValue(entity);
                if (current == null || current.Equals(default(DateTime)))
                    creationTimeProperty.SetValue(entity, DateTime.UtcNow);
            }

            await DbSet.AddAsync(entity);
            await Context.SaveChangesAsync();
            return entity;
        }
        catch (Exception ex)
        {
            LogProvider.Current.Error(ex, $"GenericStore.CreateAsync failed: {ex.Message}");
            throw;
        }
    }

    public virtual async Task<Entity> UpdateAsync(Entity entity)
    {
        try
        {
            var lastUpdatedTimeProperty = typeof(Entity).GetProperty("LastUpdatedTime");
            if (lastUpdatedTimeProperty != null)
            {
                lastUpdatedTimeProperty.SetValue(entity, DateTime.UtcNow);
            }

            var entry = Context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                // The instance came from outside the context (e.g. mapped from a request). Detach any
                // stale copy of the same row the context is already tracking, then mark this one Modified.
                // Consulting the ChangeTracker avoids the SELECT that FindAsync used to issue on every update.
                var id = GetId(entity);
                var tracked = Context.ChangeTracker.Entries<Entity>()
                    .FirstOrDefault(e => !ReferenceEquals(e.Entity, entity) && Equals(GetId(e.Entity), id));
                if (tracked != null)
                {
                    tracked.State = EntityState.Detached;
                }
                entry.State = EntityState.Modified;
            }
            // When the entity is already tracked, leave it to change tracking: EF then writes only the
            // columns that actually changed instead of every column (which rewrote password hashes and
            // salts just to bump a failed-login counter).

            await Context.SaveChangesAsync();
            return entity;
        }
        catch (Exception ex)
        {
            LogProvider.Current.Error(ex, $"GenericStore.UpdateAsync failed: {ex.Message}");
            throw;
        }
    }

    public virtual async Task<Entity> DeleteAsync(Entity entity)
    {
        try
        {
            var entry = Context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                var toDelete = await DbSet.FindAsync(GetId(entity));
                if (toDelete == null)
                {
                    throw new KeyNotFoundException($"Cannot find {typeof(Entity).Name} in the database.");
                }
                DbSet.Remove(toDelete);
            }
            else
            {
                DbSet.Remove(entity);
            }
            await Context.SaveChangesAsync();
            return entity;
        }
        catch (Exception ex) when (ex is not KeyNotFoundException)
        {
            LogProvider.Current.Error(ex, $"GenericStore.DeleteAsync failed: {ex.Message}");
            throw;
        }
    }

    public virtual async Task<bool> ExistsAsync(Expression<Func<Entity, bool>> predicate)
    {
        try
        {
            return await DbSet.AnyAsync(predicate);
        }
        catch (Exception ex)
        {
            LogProvider.Current.Error(ex, $"GenericStore.ExistsAsync failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Additional query methods

    public async Task<List<Entity>> AllPageAsync(int pageIndex, int pageSize)
        => await DbSet.Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();

    public async Task<List<Entity>> AllPageAsync(IQueryable<Entity> query, int pageIndex, int pageSize)
        => await query.Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();

    public async Task<int> CountAsync(Expression<Func<Entity, bool>> whereExpression)
        => await DbSet.Where(whereExpression).CountAsync();

    public async Task<int> CountIncludeAsync<Class>(string navigationPath, Expression<Func<Entity, bool>> whereExpression) where Class : class
        => await DbSet.Include(navigationPath).Where(whereExpression).CountAsync();

    public async Task<bool> ExistsIncludeAsync(string navigationPath, Expression<Func<Entity, bool>> whereExpression)
    {
        var parts = navigationPath.Split(',');
        var query = DbSet.Include(parts[0]);
        for (var i = 1; i < parts.Length; i++)
            query = query.Include(parts[i]);
        return await query.AnyAsync(whereExpression);
    }

    public async Task<Entity?> FindSingleAsync(Expression<Func<Entity, bool>> whereExpression)
        => await DbSet.Where(whereExpression).SingleOrDefaultAsync();

    public async Task<IEnumerable<Entity>> FindAllAsync(Expression<Func<Entity, bool>> whereExpression)
        => await DbSet.Where(whereExpression).ToListAsync();

    public async Task<List<Entity>> FindAllPageAsync(int pageIndex, int pageSize, Expression<Func<Entity, bool>> whereExpression)
        => await DbSet.Where(whereExpression).Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();

    public async Task<IEnumerable<Class>> AllIncludeAsync<Class>(string navigationPath) where Class : class
        => await DbSet.Include(navigationPath).Cast<Class>().ToListAsync();

    public async Task<Class?> FindSelectAsync<Class>(Expression<Func<Entity, bool>> whereExpression, Expression<Func<Entity, object>> selectExpression) where Class : class
        => await DbSet.Where(whereExpression).Select(selectExpression).Cast<Class>().SingleOrDefaultAsync();

    public async Task<IEnumerable<Class>> FindAllSelectAsync<Class>(Expression<Func<Entity, bool>> whereExpression, Expression<Func<Entity, object>> selectExpression) where Class : class
        => await DbSet.Where(whereExpression).Select(selectExpression).Cast<Class>().ToListAsync();

    public async Task<IEnumerable<Class>> FindAllIncludeAsync<Class>(string navigationPath, Expression<Func<Entity, bool>> whereExpression, Expression<Func<Entity, object>> selectExpression) where Class : class
        => await DbSet.Include(navigationPath).Where(whereExpression).Select(selectExpression).Cast<Class>().ToListAsync();

    public async Task<IEnumerable<Class>> FindAllIncludeAsync<Class>(string navigationPath, Expression<Func<Entity, bool>> whereExpression) where Class : class
    {
        var query = DbSet.Where(whereExpression);
        foreach (var element in navigationPath.Split(','))
            query = query.Include(element);
        return await query.Cast<Class>().ToListAsync();
    }

    public async Task<Class?> FindIncludeAsync<Class>(string navigationPath, Expression<Func<Entity, bool>> whereExpression, Expression<Func<Entity, object>> selectExpression) where Class : class
    {
        var query = DbSet.Where(whereExpression).Select(selectExpression);
        foreach (var element in navigationPath.Split(','))
            query = query.Include(element);
        return await query.Cast<Class>().SingleOrDefaultAsync();
    }

    public async Task<Entity> DeleteAsync(Guid entityId)
    {
        var entity = await DbSet.FindAsync(entityId);
        if (entity == null)
        {
            throw new KeyNotFoundException($"Cannot find {typeof(Entity).Name} with id {entityId} in the database.");
        }
        DbSet.Remove(entity);
        await Context.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> DeleteAsync(Expression<Func<Entity, bool>> whereExpression)
    {
        var toDelete = await DbSet.Where(whereExpression).ToListAsync();
        foreach (var e in toDelete)
            DbSet.Remove(e);
        await Context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Entity>> CreateRangeAsync(List<Entity> entities)
    {
        foreach (var entity in entities)
        {
            var creationTimeProperty = typeof(Entity).GetProperty("CreationTime");
            if (creationTimeProperty != null)
            {
                var current = creationTimeProperty.GetValue(entity);
                if (current == null || current.Equals(default(DateTime)))
                    creationTimeProperty.SetValue(entity, DateTime.UtcNow);
            }

            var idProperty = typeof(Entity).GetProperty("Id");
            if (idProperty != null && idProperty.PropertyType != typeof(int))
            {
                var currentId = (Guid?)idProperty.GetValue(entity);
                if (currentId == null || currentId == Guid.Empty)
                    idProperty.SetValue(entity, Guid.NewGuid());
            }
        }

        await DbSet.AddRangeAsync(entities);
        await Context.SaveChangesAsync();
        return entities;
    }

    public async Task<IEnumerable<Entity>> UpdateRangeAsync(IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            var lastUpdatedTimeProperty = typeof(Entity).GetProperty("LastUpdatedTime");
            if (lastUpdatedTimeProperty != null)
                lastUpdatedTimeProperty.SetValue(entity, DateTime.UtcNow);

            if (Context.Entry(entity).State == EntityState.Detached)
                DbSet.Attach(entity);

            Context.Entry(entity).State = EntityState.Modified;
        }

        await Context.SaveChangesAsync();
        return entities;
    }

    public async Task<int> ExecuteDeleteAsync(Expression<Func<Entity, bool>> whereExpression)
        => await DbSet.Where(whereExpression).ExecuteDeleteAsync();

    #endregion

    private Guid GetId(Entity entity)
    {
        var idProperty = typeof(Entity).GetProperty("Id")!;
        return (Guid)idProperty.GetValue(entity)!;
    }
}
