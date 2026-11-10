using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class PromotionCampaignStore : GenericStore<PromotionCampaign>, IPromotionCampaignStore
{
    public PromotionCampaignStore(CinemaContext db) : base(db)
    {
    }
}
