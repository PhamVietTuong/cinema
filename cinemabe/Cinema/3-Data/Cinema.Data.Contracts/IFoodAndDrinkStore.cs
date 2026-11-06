using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IFoodAndDrinkStore : IGenericStore<FoodAndDrink>
{
    /// <summary>Loads several items in one query (read-only), keyed by id. Unknown ids are simply absent.</summary>
    Task<Dictionary<Guid, FoodAndDrink>> GetByIdsAsync(IReadOnlyCollection<Guid> ids);

    /// <summary>
    /// Atomically adds <paramref name="delta"/> (negative = take stock) to <c>QuantityOnHand</c> in a single
    /// conditional UPDATE, so concurrent bookings can never drive it below zero. Returns false — and changes
    /// nothing — when the result would be negative. Joins whatever transaction is open on the context.
    /// </summary>
    Task<bool> TryApplyStockDeltaAsync(Guid foodAndDrinkId, int delta);
}
