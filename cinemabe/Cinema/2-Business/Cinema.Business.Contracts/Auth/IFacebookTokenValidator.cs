namespace Cinema.Business.Contracts.Auth;

/// <summary>Verified identity extracted from a Facebook access token.</summary>
public class FacebookUserInfo
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty; // Facebook's stable user id
}

/// <summary>Validates a Facebook access token via the Graph API + the configured app id/secret.</summary>
public interface IFacebookTokenValidator
{
    /// <summary>Returns the token's user info, or null if the token is invalid.</summary>
    Task<FacebookUserInfo?> ValidateAsync(string accessToken);
}
