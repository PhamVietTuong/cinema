using Cinema.Business.DTO.Loyalty;

namespace Cinema.Business.Contracts;

public interface ILoyaltySettingManager
{
    Task<EffectiveLoyaltyRateDTO> GetEffectiveAsync(Guid theaterId);
    Task<LoyaltySettingDTO> SaveAsync(SaveLoyaltySettingRequest request);
    Task<bool> ResetToDefaultAsync(Guid theaterId);
}
