using Cinema.Business.DTO;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
namespace Cinema.Business.Contracts;
public interface IAuthManager
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<UserDTO> GetProfileAsync(Guid userId);
    Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task RequestPasswordResetAsync(ForgotPasswordRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);
    Task ConfirmEmailAsync(ConfirmEmailRequest request);
    Task ResendVerificationAsync(ResendVerificationRequest request);
    Task<DefaultSearchResults<UserDTO>> GetUsersAsync(PagingSearchDTO search);
    Task<UserDTO> CreateUserAsync(CreateUserRequest request);
    Task<UserDTO> UpdateUserAsync(UpdateUserRequest request);
    Task DeleteUserAsync(Guid id);
}
