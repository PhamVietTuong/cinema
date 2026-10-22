namespace Cinema.Business.Contracts.Auth;

/// <summary>Verified identity extracted from a Facebook access token.</summary>
public class FacebookUserInfo
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty; // Facebook's stable user id
}
