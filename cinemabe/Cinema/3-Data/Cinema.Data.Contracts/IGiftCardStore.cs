using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IGiftCardStore : IGenericStore<GiftCard>
{
    Task<GiftCard?> GetByCodeAsync(string code);
}
