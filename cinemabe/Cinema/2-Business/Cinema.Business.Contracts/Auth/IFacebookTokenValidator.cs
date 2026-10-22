namespace Cinema.Business.Contracts.Auth;

/// <summary>Validates a Facebook access token via the Graph API + the configured app id/secret.</summary>
public interface IFacebookTokenValidator
{
    /// <summary>Returns the token's user info, or null if the token is invalid.</summary>
    Task<FacebookUserInfo?> ValidateAsync(string accessToken);
}
