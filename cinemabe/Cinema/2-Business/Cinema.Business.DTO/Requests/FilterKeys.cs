namespace Cinema.Business.DTO.Requests;

/// <summary>
/// Keys of <see cref="PagingSearchDTO.Filters"/> shared by every list/search endpoint. The string values are part of
/// the API contract with the frontends, so do not rename them. (The <c>theaterId</c> JWT claim is a separate concept.)
/// </summary>
public static class FilterKeys
{
    public const string Keyword = "keyword";
    public const string TheaterId = "theaterId";
}
