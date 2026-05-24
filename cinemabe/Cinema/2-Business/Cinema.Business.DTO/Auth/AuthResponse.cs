namespace Cinema.Business.DTO.Auth;
public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDTO User { get; set; } = null!;
}
