using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface INewsManager
{
    Task<DefaultSearchResults<NewsDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                          ExistsAsync(Guid id);
    Task<NewsDTO>                       GetByIdAsync(Guid id);
    Task<NewsDTO>                       CreateAsync(CreateNewsRequest request);
    Task<NewsDTO>                       UpdateAsync(UpdateNewsRequest request);
    Task                                DeleteAsync(Guid id);
}
