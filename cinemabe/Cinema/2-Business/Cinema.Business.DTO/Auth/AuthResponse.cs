namespace Cinema.Business.DTO.Auth;
public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDTO User { get; set; } = null!;
    // True when the password was valid but a 2FA code is required to finish signing in.
    // In that case Token is empty and the client must call VerifyTwoFactor.
    public bool RequiresTwoFactor { get; set; } = false;
}
