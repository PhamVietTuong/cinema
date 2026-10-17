using Cinema.Business.DTO.Loyalty;

namespace Cinema.Business.Contracts;

public interface ILoyaltySettingManager
{
    Task<LoyaltySettingDTO> GetGlobalAsync();
    Task<LoyaltySettingDTO?> GetOverrideAsync(Guid theaterId);
    Task<EffectiveLoyaltyRateDTO> GetEffectiveAsync(Guid? theaterId);
    Task<LoyaltySettingDTO> SaveAsync(SaveLoyaltySettingRequest request);
    Task<bool> ClearOverrideAsync(Guid theaterId);
}
