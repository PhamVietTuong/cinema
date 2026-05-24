namespace Cinema.Business.DTO.Auth;
public class LoginRequest
{
    public string EmailOrPhone { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
