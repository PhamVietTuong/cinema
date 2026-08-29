using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

/// <summary>Standard CRUD surface for a simple lookup ("catalog") entity.</summary>
public interface ICatalogManager<TDto, TCreate, TUpdate>
{
    Task<DefaultSearchResults<TDto>> GetAsync(PagingSearchDTO search);
    Task<bool>                       ExistsAsync(Guid id);
    Task<TDto>                       GetByIdAsync(Guid id);
    Task<TDto>                       CreateAsync(TCreate request);
    Task<TDto>                       UpdateAsync(TUpdate request);
    Task                             DeleteAsync(Guid id);
}
