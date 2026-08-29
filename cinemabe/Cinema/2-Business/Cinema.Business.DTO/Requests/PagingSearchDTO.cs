using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Requests;

public class PagingSearchDTO
{
    [Range(0, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    // Upper bound caps the result set a single call can pull. The lower bound stays 0 because the
    // managers read `PageSize > 0 ? PageSize : 20` — an explicit 0 means "use the default".
    [Range(0, 200)]
    public int PageSize  { get; set; } = 20;

    public Dictionary<string, string>? Filters { get; set; }
    public SortDTO? Sort { get; set; }
}
