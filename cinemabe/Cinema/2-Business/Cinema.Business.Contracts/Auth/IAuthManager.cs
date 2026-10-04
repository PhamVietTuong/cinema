using Cinema.Business.DTO;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
namespace Cinema.Business.Contracts.Auth;
public interface IAuthManager
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<UserDTO> GetProfileAsync(Guid userId);
    Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    Task UpdateNotificationPreferencesAsync(Guid userId, UpdateNotificationPreferencesRequest request);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task RequestPasswordResetAsync(ForgotPasswordRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);
    Task ConfirmEmailAsync(ConfirmEmailRequest request);
    Task ResendVerificationAsync(ResendVerificationRequest request);
    Task<AuthResponse> VerifyTwoFactorAsync(VerifyTwoFactorRequest request);
    Task SetTwoFactorAsync(Guid userId, bool enabled);
    Task<AuthResponse> LoginWithGoogleAsync(GoogleLoginRequest request);
    Task<AuthResponse> LoginWithFacebookAsync(FacebookLoginRequest request);
    Task<DefaultSearchResults<UserDTO>> GetUsersAsync(PagingSearchDTO search);
    Task<UserDTO> CreateUserAsync(CreateUserRequest request);
    Task<UserDTO> UpdateUserAsync(UpdateUserRequest request);
    Task DeleteUserAsync(Guid id);

    /// <summary>The theaters assigned to a RegionalManager (empty if none). 404 if the user does not exist; 400 if the user is not a RegionalManager.</summary>
    Task<UserTheatersDTO> GetUserTheatersAsync(Guid userId);

    /// <summary>Replaces a RegionalManager's theater assignments with exactly the given set (may be empty). 400 for another role or an unknown theater.</summary>
    Task<UserTheatersDTO> SetUserTheatersAsync(UserTheatersDTO request);
}
