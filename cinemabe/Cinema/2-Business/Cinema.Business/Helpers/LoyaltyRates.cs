using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Helpers;

/// <summary>
/// Resolves the effective loyalty conversion rate for a theater: that theater's own setting if one
/// exists, else the built-in defaults below (1,000 VND spent = 1 point; 100 points = 1,000 VND,
/// i.e. 1 point = 10 VND). There is no global setting.
/// </summary>
public static class LoyaltyRates
{
    public const int DefaultPointsPerUnit = 1000;
    public const int DefaultPointValueVnd = 10;

    public static async Task<(int PointsPerUnit, int PointValueVnd)> LoadAsync(IApplicationUnitOfWork uow, Guid? theaterId)
    {
        LoyaltySetting? setting = null;
        if (theaterId is Guid id)
        {
            setting = await uow.LoyaltySettingStore.GetOverrideAsync(id);
        }

        return Resolve(setting);
    }

    /// <summary>Pure merge step shared by <see cref="LoadAsync"/> and callers that already hold the
    /// row (so they don't need to query it again) — a null or non-positive value falls back to the
    /// built-in default.</summary>
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
