using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IGiftCardStore : IGenericStore<GiftCard>
{
    Task<GiftCard?> GetByCodeAsync(string code);

    /// <summary>Atomically draws <paramref name="amount"/> from the card: UPDATE ... SET Balance = Balance - a
    /// WHERE Balance >= a. Returns false when the balance no longer covers it, so a card cannot be overspent
    /// by two concurrent bookings.</summary>
    Task<bool> TryDrawBalanceAsync(Guid cardId, double amount);
}
