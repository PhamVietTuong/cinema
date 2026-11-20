using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IShowTimeManager
{
    Task<DefaultSearchResults<ShowTimeDTO>> GetAsync(PagingSearchDTO search);
    /// <summary>Slim week-calendar rows. Filters: movieId, theaterId, projectionForm, from, to (StartTime, half-open).</summary>
    Task<DefaultSearchResults<ShowTimeCalendarItemDTO>> GetCalendarAsync(PagingSearchDTO search);
    Task<bool>                              ExistsAsync(Guid id);
    Task<ShowTimeDTO>                       GetByIdAsync(Guid id);
    Task<ShowTimeDTO>                       CreateAsync(CreateShowTimeRequest request);
    Task<ShowTimeDTO>                       UpdateAsync(UpdateShowTimeRequest request);
    Task                                    DeleteAsync(Guid id);
}
