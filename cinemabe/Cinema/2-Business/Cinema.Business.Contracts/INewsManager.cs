using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface INewsManager
{
    /// <param name="publishedOnly">True for callers who may not see drafts (everyone except an admin): drafts are
    /// excluded in SQL regardless of any <c>isPublished</c> filter the caller sends.</param>
    Task<DefaultSearchResults<NewsDTO>> GetAsync(PagingSearchDTO search, bool publishedOnly = false);
    Task<bool>                          ExistsAsync(Guid id);
    /// <param name="publishedOnly">True for callers who may not see drafts: a draft is reported as not found.</param>
    Task<NewsDTO>                       GetByIdAsync(Guid id, bool publishedOnly = false);
    Task<NewsDTO>                       CreateAsync(CreateNewsRequest request);
    Task<NewsDTO>                       UpdateAsync(UpdateNewsRequest request);
    Task                                DeleteAsync(Guid id);
}
