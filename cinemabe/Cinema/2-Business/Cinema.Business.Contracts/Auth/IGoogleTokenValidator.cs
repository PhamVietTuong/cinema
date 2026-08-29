namespace Cinema.Business.Contracts.Auth;

/// <summary>Verified identity extracted from a Google ID token.</summary>
public class GoogleUserInfo
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string Subject { get; set; } = string.Empty; // Google's stable user id
}

/// <summary>Validates a Google ID token against Google's keys + the configured client id.</summary>
public interface IGoogleTokenValidator
{
    /// <summary>Returns the token's user info, or null if the token is invalid.</summary>
    Task<GoogleUserInfo?> ValidateAsync(string idToken);
}
