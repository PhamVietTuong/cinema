using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
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
}
