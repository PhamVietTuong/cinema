namespace Cinema.Data.Stores;

/// <summary>
/// The two paging conventions the stores already use, named so a call site says which one it means. The API list
/// endpoints (<c>PagingSearchDTO.PageIndex</c>) and <c>GenericStore</c> are 0-based; the store-specific searches that
/// take a <c>page</c> argument are 1-based. Both keep the arithmetic exactly as it was: no clamping.
/// </summary>
public static class QueryablePagingExtensions
{
    /// <summary>Rows of page <paramref name="page"/> where the first page is 1.</summary>
    public static IQueryable<T> PageOneBased<T>(this IQueryable<T> query, int page, int pageSize)
    {
        return query.Skip((page - 1) * pageSize).Take(pageSize);
    }

    /// <summary>Rows of page <paramref name="pageIndex"/> where the first page is 0.</summary>
    public static IQueryable<T> PageZeroBased<T>(this IQueryable<T> query, int pageIndex, int pageSize)
    {
        return query.Skip(pageIndex * pageSize).Take(pageSize);
    }
}
