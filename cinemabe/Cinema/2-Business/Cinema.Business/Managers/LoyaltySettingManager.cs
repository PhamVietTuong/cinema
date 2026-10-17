using Cinema.Business.Contracts;
using Cinema.Business.DTO.Loyalty;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class LoyaltySettingManager : ILoyaltySettingManager
{
    private readonly IApplicationUnitOfWork _uow;

    public LoyaltySettingManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<LoyaltySettingDTO> GetGlobalAsync()
    {
        var setting = await _uow.LoyaltySettingStore.GetGlobalAsync();
        return ToDTO(setting, null);
    }

    public async Task<LoyaltySettingDTO?> GetOverrideAsync(Guid theaterId)
    {
        var setting = await _uow.LoyaltySettingStore.GetOverrideAsync(theaterId);
        if (setting is null)
        {
            return null;
        }
        return ToDTO(setting, theaterId);
    }

    public async Task<EffectiveLoyaltyRateDTO> GetEffectiveAsync(Guid? theaterId)
    {
        LoyaltySetting? overrideSetting = null;
        if (theaterId is Guid id)
        {
            overrideSetting = await _uow.LoyaltySettingStore.GetOverrideAsync(id);
        }

        var setting = overrideSetting;
        if (setting is null)
        {
            setting = await _uow.LoyaltySettingStore.GetGlobalAsync();
        }

        var (pointsPerUnit, pointValueVnd) = LoyaltyRates.Resolve(setting);
        return new EffectiveLoyaltyRateDTO
        {
            PointsPerUnit = pointsPerUnit,
            PointValueVnd = pointValueVnd,
            IsOverride    = overrideSetting is not null,
        };
    }

    public async Task<LoyaltySettingDTO> SaveAsync(SaveLoyaltySettingRequest request)
    {
        if (request.PointsPerUnit <= 0 || request.PointValueVnd <= 0)
        {
            throw new InvalidOperationException("Point rates must be positive.");
        }

        LoyaltySetting? existing;
        if (request.TheaterId is Guid theaterId)
        {
            if (!await _uow.TheaterStore.ExistsAsync(t => t.Id == theaterId))
            {
                throw new KeyNotFoundException("Theater not found.");
            }
            existing = await _uow.LoyaltySettingStore.GetOverrideAsync(theaterId);
        }
        else
        {
            existing = await _uow.LoyaltySettingStore.GetGlobalAsync();
        }

        if (existing is null)
        {
            var created = new LoyaltySetting
            {
                TheaterId     = request.TheaterId,
                PointsPerUnit = request.PointsPerUnit,
                PointValueVnd = request.PointValueVnd,
            };
            await _uow.LoyaltySettingStore.CreateAsync(created);
            return ToDTO(created, request.TheaterId);
        }

        existing.PointsPerUnit = request.PointsPerUnit;
        existing.PointValueVnd = request.PointValueVnd;
        await _uow.LoyaltySettingStore.UpdateAsync(existing);
        return ToDTO(existing, request.TheaterId);
    }

    public async Task<bool> ClearOverrideAsync(Guid theaterId)
    {
        var existing = await _uow.LoyaltySettingStore.GetOverrideAsync(theaterId);
        if (existing is null)
        {
            return false;
        }
        await _uow.LoyaltySettingStore.DeleteAsync(existing);
        return true;
    }

    private static LoyaltySettingDTO ToDTO(LoyaltySetting? setting, Guid? theaterId)
    {
        var (pointsPerUnit, pointValueVnd) = LoyaltyRates.Resolve(setting);
        DateTime? lastUpdatedTime = null;
        if (setting is not null)
        {
            lastUpdatedTime = setting.LastUpdatedTime;
        }

        return new LoyaltySettingDTO
        {
            TheaterId       = theaterId,
            PointsPerUnit   = pointsPerUnit,
            PointValueVnd   = pointValueVnd,
            LastUpdatedTime = lastUpdatedTime,
        };
    }
}
