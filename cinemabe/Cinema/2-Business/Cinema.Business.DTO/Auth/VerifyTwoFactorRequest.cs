namespace Cinema.Business.DTO.Auth;
public class VerifyTwoFactorRequest
{
    public string EmailOrPhone { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
