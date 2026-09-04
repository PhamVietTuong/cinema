using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IPatronCategoryManager
{
    Task<DefaultSearchResults<PatronCategoryDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                                    ExistsAsync(Guid id);
    Task<PatronCategoryDTO>                       GetByIdAsync(Guid id);
    Task<PatronCategoryDTO>                       CreateAsync(CreatePatronCategoryRequest request);
    Task<PatronCategoryDTO>                       UpdateAsync(UpdatePatronCategoryRequest request);
    Task                                          DeleteAsync(Guid id);
}
