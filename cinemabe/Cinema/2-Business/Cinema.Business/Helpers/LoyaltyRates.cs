using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Helpers;

/// <summary>
/// Resolves the effective loyalty conversion rate for a theater: that theater's override if one
/// exists, else the global default row, else these hardcoded constants as a last-resort safety net
/// (the table should never actually be empty once seeded).
/// </summary>
public static class LoyaltyRates
{
    public const int DefaultPointsPerUnit = 1000;
    public const int DefaultPointValueVnd = 1000;

    public static async Task<(int PointsPerUnit, int PointValueVnd)> LoadAsync(IApplicationUnitOfWork uow, Guid? theaterId)
    {
        LoyaltySetting? setting = null;
        if (theaterId is Guid id)
        {
            setting = await uow.LoyaltySettingStore.GetOverrideAsync(id);
        }
        if (setting is null)
        {
            setting = await uow.LoyaltySettingStore.GetGlobalAsync();
        }

        return Resolve(setting);
    }

    /// <summary>Pure merge step shared by <see cref="LoadAsync"/> and callers that already hold the
    /// row (so they don't need to query it again) — a null or non-positive value falls back to the
    /// hardcoded default.</summary>
    public static (int PointsPerUnit, int PointValueVnd) Resolve(LoyaltySetting? setting)
    {
        int pointsPerUnit;
        if (setting is { PointsPerUnit: > 0 })
        {
            pointsPerUnit = setting.PointsPerUnit;
        }
        else
        {
            pointsPerUnit = DefaultPointsPerUnit;
        }

        int pointValueVnd;
        if (setting is { PointValueVnd: > 0 })
        {
            pointValueVnd = setting.PointValueVnd;
        }
        else
        {
            pointValueVnd = DefaultPointValueVnd;
        }

        return (pointsPerUnit, pointValueVnd);
    }
}
