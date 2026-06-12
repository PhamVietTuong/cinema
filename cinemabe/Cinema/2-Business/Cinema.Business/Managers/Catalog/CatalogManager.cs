using System.Reflection;
using Cinema.Business.Contracts;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

/// <summary>
/// Generic CRUD manager for simple lookup ("catalog") entities. Concrete managers
/// supply the backing store and an optional keyword <see cref="Match"/> predicate.
/// </summary>
public abstract class CatalogManager<TEntity, TDto, TCreate, TUpdate>
    : ICatalogManager<TDto, TCreate, TUpdate>
    where TEntity : BaseEntity, new()
    where TDto : new()
    where TUpdate : IHasId
{
    protected readonly IApplicationUnitOfWork Uow;

    protected CatalogManager(IApplicationUnitOfWork uow)
    {
        Uow = uow;
    }

    /// <summary>The store backing this entity (e.g. <c>Uow.AgeRestrictionStore</c>).</summary>
    protected abstract IGenericStore<TEntity> Store { get; }

    /// <summary>Keyword filter for the list endpoint. Default matches everything.</summary>
    protected virtual bool Match(TEntity entity, string keyword)
    {
        return true;
    }

    public virtual async Task<DefaultSearchResults<TDto>> GetAsync(PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var all = (await Store.GetAllAsync()).ToList();

        // Free-text keyword search across the entity's configured fields.
        var keyword = search.Filters.GetString("keyword");
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            all = all.Where(e => Match(e, keyword)).ToList();
        }

        // Per-column filters: any filter key matching an entity property name.
        all = ApplyColumnFilters(all, search.Filters);

        var page = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize = search.PageSize > 0 ? search.PageSize : 20;
        var total = all.Count;
        var paged = all
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => e.ToDTO<TEntity, TDto>())
            .ToList();

        return new DefaultSearchResults<TDto>
        {
            Results = paged,
            TotalCount = total,
            CountPerPage = pageSize,
            Page = page
        };
    }

    public virtual async Task<TDto> GetByIdAsync(Guid id)
    {
        var entity = await Store.GetByIdAsync(id)
                     ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} {id} not found.");
        return entity.ToDTO<TEntity, TDto>();
    }

    public virtual async Task<TDto> CreateAsync(TCreate request)
    {
        var entity = request!.ToNewEntity<TCreate, TEntity>();
        await Store.CreateAsync(entity);
        return entity.ToDTO<TEntity, TDto>();
    }

    public virtual async Task<TDto> UpdateAsync(TUpdate request)
    {
        var entity = await Store.GetByIdAsync(request.Id)
                     ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} {request.Id} not found.");
        entity.PatchEntity<TEntity, TUpdate>(request);
        await Store.UpdateAsync(entity);
        return entity.ToDTO<TEntity, TDto>();
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        await Store.DeleteAsync(id);
    }

    /// <summary>
    /// Applies a case-insensitive "contains" filter for every filter entry whose key
    /// matches a public property on the entity (e.g. <c>{ "name": "vip" }</c>). The
    /// reserved "keyword" key is handled separately and skipped here.
    /// </summary>
    private static List<TEntity> ApplyColumnFilters(List<TEntity> items, Dictionary<string, string>? filters)
    {
        if (filters == null || filters.Count == 0)
        {
            return items;
        }

        var props = typeof(TEntity)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, raw) in filters)
        {
            if (string.Equals(key, "keyword", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(raw) || !props.TryGetValue(key, out var prop))
            {
                continue;
            }

            var needle = raw.Trim();
            items = items.Where(e =>
            {
                var value = prop.GetValue(e)?.ToString();
                return value != null && value.Contains(needle, StringComparison.OrdinalIgnoreCase);
            }).ToList();
        }

        return items;
    }
}
