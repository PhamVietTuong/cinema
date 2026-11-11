using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IUserStore : IGenericStore<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByPhoneAsync(string phone);

    /// <summary>Atomically spends loyalty points: a single UPDATE ... SET Points = Points - n WHERE Points >= n.
    /// Returns false when the balance is too low (e.g. a concurrent booking already spent it), so the same
    /// points can never be redeemed twice.</summary>
    Task<bool> TryDeductPointsAsync(Guid userId, int points);
    Task<(IEnumerable<User> Items, int Total)> GetPagedAsync(string? search, int page, int pageSize, Guid? theaterId = null);

    /// <summary>Display name by user id for a batch of ids (one query). Unknown ids are absent.</summary>
    Task<Dictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids);
}
