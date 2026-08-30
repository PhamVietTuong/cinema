using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IDiscountTypeManager
{
    Task<DefaultSearchResults<DiscountTypeDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                                  ExistsAsync(Guid id);
    Task<DiscountTypeDTO>                       GetByIdAsync(Guid id);
    Task<DiscountTypeDTO>                       CreateAsync(CreateDiscountTypeRequest request);
    Task<DiscountTypeDTO>                       UpdateAsync(UpdateDiscountTypeRequest request);
    Task                                        DeleteAsync(Guid id);
}
