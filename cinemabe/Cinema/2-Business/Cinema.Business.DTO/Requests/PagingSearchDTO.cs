namespace Cinema.Business.DTO.Requests;

public class PagingSearchDTO
{
    public int PageIndex { get; set; } = 1;
    public int PageSize  { get; set; } = 20;
    public Dictionary<string, string>? Filters { get; set; }
    public SortDTO? Sort { get; set; }
}
