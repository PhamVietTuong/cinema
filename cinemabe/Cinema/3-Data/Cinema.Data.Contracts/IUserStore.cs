using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IUserStore : IGenericStore<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByPhoneAsync(string phone);
    Task<(IEnumerable<User> Items, int Total)> GetPagedAsync(string? search, int page, int pageSize);
}
