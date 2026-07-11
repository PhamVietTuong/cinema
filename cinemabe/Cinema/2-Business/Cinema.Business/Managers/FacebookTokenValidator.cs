using System.Text.Json;
using Cinema.Business.Contracts;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Managers;

/// <summary>
/// Validates a Facebook access token against the Graph API. When an app id + secret are
/// configured, the token is first verified with <c>debug_token</c> to confirm it was
/// issued for this app; then the user's profile is fetched from <c>/me</c>.
/// </summary>
public class FacebookTokenValidator : IFacebookTokenValidator
{
    private static readonly HttpClient _http = new();
    private const string GraphBase = "https://graph.facebook.com/v19.0";

    private readonly IConfiguration _config;

    public FacebookTokenValidator(IConfiguration config) => _config = config;

    public async Task<FacebookUserInfo?> ValidateAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        var appId = _config["Facebook:AppId"];
        if (string.IsNullOrWhiteSpace(appId))
            throw new InvalidOperationException("Facebook login is not configured (Facebook:AppId is missing).");

        var appSecret = _config["Facebook:AppSecret"];

        // If the app secret is configured, verify the token belongs to this app.
        if (!string.IsNullOrWhiteSpace(appSecret))
        {
            var appToken = $"{appId}|{appSecret}";
            using var debug = await _http.GetAsync(
                $"{GraphBase}/debug_token?input_token={Uri.EscapeDataString(accessToken)}&access_token={Uri.EscapeDataString(appToken)}");
            if (!debug.IsSuccessStatusCode)
                return null;
            using var debugDoc = JsonDocument.Parse(await debug.Content.ReadAsStringAsync());
            var data = debugDoc.RootElement.GetProperty("data");
            var isValid = data.TryGetProperty("is_valid", out var v) && v.GetBoolean();
            var tokenAppId = data.TryGetProperty("app_id", out var a) ? a.GetString() : null;
            if (!isValid || tokenAppId != appId)
                return null;
        }

        using var me = await _http.GetAsync(
            $"{GraphBase}/me?fields=id,name,email&access_token={Uri.EscapeDataString(accessToken)}");
        if (!me.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
        if (string.IsNullOrWhiteSpace(email))
            return null; // No email permission granted — can't create/link an account.

        var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
        var id = root.TryGetProperty("id", out var i) ? i.GetString() : null;
        return new FacebookUserInfo
        {
            Email = email,
            Name = string.IsNullOrWhiteSpace(name) ? email : name!,
            Subject = id ?? string.Empty,
        };
    }
}
