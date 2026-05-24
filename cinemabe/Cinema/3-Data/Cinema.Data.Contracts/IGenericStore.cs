using System.Linq.Expressions;
using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IGenericStore<Entity> where Entity : BaseEntity
{
    // ── Query builders ────────────────────────────────────────────────────────

    IQueryable<Entity> GetQuery();
    IQueryable<Entity> GetQuery(string linkedElements);
    IQueryable<Entity> FilterQuery(IQueryable<Entity> query, Expression<Func<Entity, bool>> whereExpression);
    IQueryable<Entity> FilterQuery(IQueryable<Entity> query, string navigationPath, Expression<Func<Entity, bool>> whereExpression);
    IOrderedQueryable<Entity> OrderQuery<T>(IQueryable<Entity> query, Expression<Func<Entity, T>> keySelector, bool isAscending = true);
    IOrderedQueryable<Entity> OrderQuery<T>(IOrderedQueryable<Entity> query, Expression<Func<Entity, T>> keySelector, bool isAscending = true);

    // ── CRUD ──────────────────────────────────────────────────────────────────

    Task<Entity?> GetByIdAsync(Guid id);
    Task<IEnumerable<Entity>> GetAllAsync();
    Task<IEnumerable<Entity>> FindAsync(Expression<Func<Entity, bool>> predicate);
    Task<Entity> CreateAsync(Entity entity);
    Task<Entity> UpdateAsync(Entity entity);
    Task<Entity> DeleteAsync(Entity entity);
    Task<bool> ExistsAsync(Expression<Func<Entity, bool>> predicate);

    // ── Paged / count ─────────────────────────────────────────────────────────

    Task<List<Entity>> AllPageAsync(int pageIndex, int pageSize);
    Task<List<Entity>> AllPageAsync(IQueryable<Entity> query, int pageIndex, int pageSize);
    Task<int> CountAsync(Expression<Func<Entity, bool>> whereExpression);
    Task<int> CountIncludeAsync<Class>(string navigationPath, Expression<Func<Entity, bool>> whereExpression) where Class : class;

    // ── Include / filter helpers ──────────────────────────────────────────────

    Task<bool> ExistsIncludeAsync(string navigationPath, Expression<Func<Entity, bool>> whereExpression);
    Task<Entity?> FindSingleAsync(Expression<Func<Entity, bool>> whereExpression);
    Task<IEnumerable<Entity>> FindAllAsync(Expression<Func<Entity, bool>> whereExpression);
    Task<List<Entity>> FindAllPageAsync(int pageIndex, int pageSize, Expression<Func<Entity, bool>> whereExpression);
    Task<IEnumerable<Class>> AllIncludeAsync<Class>(string navigationPath) where Class : class;
    Task<Class?> FindSelectAsync<Class>(Expression<Func<Entity, bool>> whereExpression, Expression<Func<Entity, object>> selectExpression) where Class : class;
    Task<IEnumerable<Class>> FindAllSelectAsync<Class>(Expression<Func<Entity, bool>> whereExpression, Expression<Func<Entity, object>> selectExpression) where Class : class;
    Task<IEnumerable<Class>> FindAllIncludeAsync<Class>(string navigationPath, Expression<Func<Entity, bool>> whereExpression, Expression<Func<Entity, object>> selectExpression) where Class : class;
    Task<IEnumerable<Class>> FindAllIncludeAsync<Class>(string navigationPath, Expression<Func<Entity, bool>> whereExpression) where Class : class;
    Task<Class?> FindIncludeAsync<Class>(string navigationPath, Expression<Func<Entity, bool>> whereExpression, Expression<Func<Entity, object>> selectExpression) where Class : class;

    // ── Bulk delete ───────────────────────────────────────────────────────────

    Task<Entity> DeleteAsync(Guid entityId);
    Task<bool> DeleteAsync(Expression<Func<Entity, bool>> whereExpression);
    Task<int> ExecuteDeleteAsync(Expression<Func<Entity, bool>> whereExpression);

    // ── Range operations ──────────────────────────────────────────────────────

    Task<List<Entity>> CreateRangeAsync(List<Entity> entities);
    Task<IEnumerable<Entity>> UpdateRangeAsync(IEnumerable<Entity> entities);
}
