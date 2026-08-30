using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IAgeRestrictionManager
{
    Task<DefaultSearchResults<AgeRestrictionDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                                    ExistsAsync(Guid id);
    Task<AgeRestrictionDTO>                       GetByIdAsync(Guid id);
    Task<AgeRestrictionDTO>                       CreateAsync(CreateAgeRestrictionRequest request);
    Task<AgeRestrictionDTO>                       UpdateAsync(UpdateAgeRestrictionRequest request);
    Task                                          DeleteAsync(Guid id);
}
