using System.Net.Http.Headers;
using System.Text;
using Cinema.Business.Contracts;
using Cinema.Foundation.Logging;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Notifications;

/// <summary>
/// Sends SMS via Twilio's REST API (no SDK dependency). Config under "Sms:Twilio": AccountSid, AuthToken,
/// FromNumber. Registered only when AccountSid is set; otherwise the dev-log sender is used. Failures are
/// logged, never thrown, so an SMS outage can't break auth/booking flows (mirrors SmtpNotificationService).
/// </summary>
public class TwilioSmsNotificationService : ISmsNotificationService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;

    public TwilioSmsNotificationService(IConfiguration config, IHttpClientFactory httpFactory)
    {
        _config = config;
        _httpFactory = httpFactory;
    }

    public async Task SendSmsAsync(string phoneNumber, string message)
    {
        var sid   = _config["Sms:Twilio:AccountSid"];
        var token = _config["Sms:Twilio:AuthToken"];
        var from  = _config["Sms:Twilio:FromNumber"];
        if (string.IsNullOrWhiteSpace(sid) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(phoneNumber))
        {
            LogProvider.Current.Information($"[SMS:skipped] To={phoneNumber}");
            return;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.twilio.com/2010-04-01/Accounts/{sid}/Messages.json")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["To"]   = phoneNumber,
                    ["From"] = from ?? string.Empty,
                    ["Body"] = message,
                }),
            };
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sid}:{token}"));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            var http = _httpFactory.CreateClient();
            using var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                LogProvider.Current.Information($"TwilioSmsNotificationService->non-success {(int)resp.StatusCode}: {body}");
            }
        }
        catch (Exception e)
        {
            LogProvider.Current.Fatal(e, $"TwilioSmsNotificationService->Exception sending to {phoneNumber}: {e.Message}");
        }
    }
}
