using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IDiscountStore : IGenericStore<Discount>
{
    /// <summary>All discounts with their theater scope loaded (for admin listing/edit).</summary>
    Task<List<Discount>> GetAllWithScopeAsync();
    /// <summary>A single discount with its theater scope loaded.</summary>
    Task<Discount?> GetByIdWithScopeAsync(Guid id);
    /// <summary>Looks up a discount by promo code, theater scope loaded.</summary>
    Task<Discount?> GetByCodeAsync(string code);
    /// <summary>Active, auto-apply promotions whose date window contains <paramref name="now"/>, theater scope loaded.</summary>
    Task<List<Discount>> GetActiveAutoApplyAsync(DateTime now);
}
