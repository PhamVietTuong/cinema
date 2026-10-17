using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface ITemplateManager
{
    Task<DefaultSearchResults<TemplateDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                              ExistsAsync(Guid id);
    Task<TemplateDTO>                       GetByIdAsync(Guid id);
    Task<TemplateDTO>                       CreateAsync(CreateTemplateRequest request);
    Task<TemplateDTO>                       UpdateAsync(UpdateTemplateRequest request);
    Task                                    DeleteAsync(Guid id);
}
