using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface IPromotionCampaignManager
{
    Task<DefaultSearchResults<PromotionCampaignDTO>> GetAsync(PagingSearchDTO search);
    Task<bool>                                       ExistsAsync(Guid id);
    Task<PromotionCampaignDTO>                       GetByIdAsync(Guid id);
    Task<PromotionCampaignDTO>                       CreateAsync(CreatePromotionCampaignRequest request);
    Task<PromotionCampaignDTO>                       UpdateAsync(UpdatePromotionCampaignRequest request);
    Task                                              DeleteAsync(Guid id);
}
