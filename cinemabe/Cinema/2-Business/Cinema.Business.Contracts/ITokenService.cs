using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

public interface ITokenService
{
    string GenerateToken(User user);
    DateTime GetTokenExpiry();
}
