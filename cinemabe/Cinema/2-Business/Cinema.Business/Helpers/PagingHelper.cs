using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Helpers;

/// <summary>Shared paging plumbing for catalog list endpoints (page/size defaults, DTO-result wrapping).</summary>
public static class PagingHelper
{
    /// <summary>Resolves the 1-based page index / page size from a search request, applying defaults.</summary>
    public static (int Page, int PageSize) ResolvePaging(PagingSearchDTO search)
    {
        var page = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize = search.PageSize > 0 ? search.PageSize : 20;
        return (page, pageSize);
    }

    /// <summary>Wraps a page of entities into the standard paged DTO result.</summary>
    public static DefaultSearchResults<TDto> ToPagedResult<TEntity, TDto>(List<TEntity> items, int total, int page, int pageSize) where TDto : new()
    {
        return new DefaultSearchResults<TDto>
        {
            Results = items.Select(e => e.ToDTO<TEntity, TDto>()).ToList(),
            TotalCount = total,
            CountPerPage = pageSize,
            Page = page
        };
    }

    /// <summary>
    /// The catalog list pipeline in one place: default the request, build the filtered query, sort it, count it, read the
    /// requested page and wrap it as DTOs. Each manager supplies only its own filter and sort (its sort whitelist stays
    /// with the entity, so callers still cannot order by an arbitrary column).
    /// </summary>
    public static async Task<DefaultSearchResults<TDto>> SearchAsync<TEntity, TDto>(
        IGenericStore<TEntity> store,
        PagingSearchDTO? search,
        Func<Dictionary<string, string>?, IQueryable<TEntity>> filter,
        Func<IQueryable<TEntity>, SortDTO?, IQueryable<TEntity>> sort)
        where TEntity : BaseEntity
        where TDto : new()
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = ResolvePaging(search);

        var query = sort(filter(search.Filters), search.Sort);
        var total = await store.CountAsync(query);
        var items = await store.AllPageAsync(query, page - 1, pageSize);
        return ToPagedResult<TEntity, TDto>(items, total, page, pageSize);
    }
}
