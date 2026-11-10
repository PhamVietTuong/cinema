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

    public async Task<EffectiveLoyaltyRateDTO> GetEffectiveAsync(Guid theaterId)
    {
        var setting = await _uow.LoyaltySettingStore.GetOverrideAsync(theaterId);

        var (pointsPerUnit, pointValueVnd) = LoyaltyRates.Resolve(setting);
        return new EffectiveLoyaltyRateDTO
        {
            PointsPerUnit = pointsPerUnit,
            PointValueVnd = pointValueVnd,
            IsCustom      = setting is not null,
        };
    }

    public async Task<LoyaltySettingDTO> SaveAsync(SaveLoyaltySettingRequest request)
    {
        if (request.PointsPerUnit <= 0 || request.PointValueVnd <= 0)
        {
            throw new InvalidOperationException("Point rates must be positive.");
        }

        if (!await _uow.TheaterStore.ExistsAsync(t => t.Id == request.TheaterId))
        {
            throw new KeyNotFoundException("Theater not found.");
        }
        var existing = await _uow.LoyaltySettingStore.GetOverrideAsync(request.TheaterId);

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

    public async Task<bool> ResetToDefaultAsync(Guid theaterId)
    {
        var existing = await _uow.LoyaltySettingStore.GetOverrideAsync(theaterId);
        if (existing is null)
        {
            return false;
        }
        await _uow.LoyaltySettingStore.DeleteAsync(existing);
        return true;
    }

    private static LoyaltySettingDTO ToDTO(LoyaltySetting? setting, Guid theaterId)
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
