namespace Cinema.Business.Contracts.Auth;

/// <summary>Validates a Google ID token against Google's keys + the configured client id.</summary>
public interface IGoogleTokenValidator
{
    /// <summary>Returns the token's user info, or null if the token is invalid.</summary>
    Task<GoogleUserInfo?> ValidateAsync(string idToken);
}
