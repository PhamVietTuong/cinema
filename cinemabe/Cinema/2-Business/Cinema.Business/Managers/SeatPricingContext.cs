using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

internal sealed record SeatPricingContext(
    IReadOnlyDictionary<Guid, ResolvedCategory> ByCategoryId,
    IReadOnlyDictionary<SeatKind, double> MinPriceByKind);
