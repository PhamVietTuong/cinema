using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IUserStore : IGenericStore<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByPhoneAsync(string phone);
    Task<(IEnumerable<User> Items, int Total)> GetPagedAsync(string? search, int page, int pageSize);

    /// <summary>Display name by user id for a batch of ids (one query). Unknown ids are absent.</summary>
    Task<Dictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids);
}
