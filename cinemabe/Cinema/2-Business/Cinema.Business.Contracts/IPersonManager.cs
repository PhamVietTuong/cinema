using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IPersonManager
{
    Task<DefaultSearchResults<PersonDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                            ExistsAsync(Guid id);
    Task<PersonDTO>                       GetByIdAsync(Guid id);
    Task<PersonDTO>                       CreateAsync(CreatePersonRequest request);
    Task<PersonDTO>                       UpdateAsync(UpdatePersonRequest request);
    Task                                  DeleteAsync(Guid id);
    Task<List<PersonDTO>>                 GetByNamesAsync(IList<string> names);
}
