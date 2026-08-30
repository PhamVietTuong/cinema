using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IShowTimeManager
{
    Task<DefaultSearchResults<ShowTimeDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                              ExistsAsync(Guid id);
    Task<ShowTimeDTO>                       GetByIdAsync(Guid id);
    Task<ShowTimeDTO>                       CreateAsync(CreateShowTimeRequest request);
    Task<ShowTimeDTO>                       UpdateAsync(UpdateShowTimeRequest request);
    Task                                    DeleteAsync(Guid id);
}
