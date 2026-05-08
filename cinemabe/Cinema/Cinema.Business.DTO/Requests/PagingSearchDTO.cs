namespace Cinema.Business.DTO.Requests
{
    public class PagingSearchDTO
    {
        public int PageIndex { get; set; }

        public int PageSize { get; set; }

        public Dictionary<string, string> Filters { get; set; }

        public SortDTO Sort { get; set; }
    }
}
