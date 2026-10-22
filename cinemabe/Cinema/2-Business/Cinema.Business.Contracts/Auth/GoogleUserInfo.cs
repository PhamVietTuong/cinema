namespace Cinema.Business.Contracts.Auth;

/// <summary>Verified identity extracted from a Google ID token.</summary>
public class GoogleUserInfo
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string Subject { get; set; } = string.Empty; // Google's stable user id
}
