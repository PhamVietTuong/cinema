using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IDiscountManager
{
    Task<DefaultSearchResults<DiscountDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                              ExistsAsync(Guid id);
    Task<DiscountDTO>                       GetByIdAsync(Guid id);
    Task<DiscountDTO>                       CreateAsync(CreateDiscountRequest request);
    Task<DiscountDTO>                       UpdateAsync(UpdateDiscountRequest request);
    Task                                    DeleteAsync(Guid id);
}
