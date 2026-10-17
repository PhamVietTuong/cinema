using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface ILoyaltySettingStore : IGenericStore<LoyaltySetting>
{
    Task<LoyaltySetting?> GetGlobalAsync();
    Task<LoyaltySetting?> GetOverrideAsync(Guid theaterId);
}
