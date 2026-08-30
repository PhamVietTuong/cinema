using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IUserTypeManager
{
    Task<DefaultSearchResults<UserTypeDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                              ExistsAsync(Guid id);
    Task<UserTypeDTO>                       GetByIdAsync(Guid id);
    Task<UserTypeDTO>                       CreateAsync(CreateUserTypeRequest request);
    Task<UserTypeDTO>                       UpdateAsync(UpdateUserTypeRequest request);
    Task                                    DeleteAsync(Guid id);
}
