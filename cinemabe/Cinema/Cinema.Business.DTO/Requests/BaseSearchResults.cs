
namespace Cinema.Business.DTO.Requests
{
    using System.Collections.Generic;

    public abstract class BaseSearchResults<T>
    {
        public IEnumerable<T> Results { get; set; }

        public int ResultsCount()
        {
            if (Results == null)
            {
                {
                    return 0;
                }
            }
            int i = 0;
            IEnumerator<T> enumerator = Results.GetEnumerator();
            while (enumerator.MoveNext())
            {
                i++;
            }

            return i;
        }
        public int TotalCount { get; set; }
        public int CountPerPage { get; set; }
        public int Page { get; set; }
    }
}
