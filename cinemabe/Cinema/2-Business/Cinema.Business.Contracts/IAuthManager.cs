using Cinema.Business.DTO;
using Cinema.Business.DTO.Auth;
namespace Cinema.Business.Contracts;
public interface IAuthManager
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<UserDTO> GetProfileAsync(Guid userId);
    Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
}
