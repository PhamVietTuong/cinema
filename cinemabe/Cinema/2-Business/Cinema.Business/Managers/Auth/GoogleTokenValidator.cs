using Cinema.Business.Contracts.Auth;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Managers.Auth;

/// <summary>Validates Google ID tokens using Google's published keys and the configured client id.</summary>
public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly IConfiguration _config;

    public GoogleTokenValidator(IConfiguration config)
    {
        _config = config;
    }

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken)
    {
        var clientId = _config["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Google login is not configured (Google:ClientId is missing).");
        }

        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { clientId }
        };

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return new GoogleUserInfo
            {
                Email = payload.Email,
                Name = string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name,
                EmailVerified = payload.EmailVerified,
                Subject = payload.Subject,
            };
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
