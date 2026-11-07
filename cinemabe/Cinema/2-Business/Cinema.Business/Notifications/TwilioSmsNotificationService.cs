using System.Net.Http.Headers;
using System.Text;
using Cinema.Business.Contracts;
using Cinema.Foundation.Logging;

namespace Cinema.Business.Notifications;

/// <summary>
/// Sends SMS via Twilio's REST API (no SDK dependency). Config under "Sms:Twilio" (see <see cref="TwilioOptions"/>,
/// validated at startup): AccountSid, AuthToken, FromNumber. Recipients are normalised to E.164 first (an invalid
/// number is logged and skipped) and each request is capped by TimeoutSeconds. Registered only when AccountSid
/// is set; otherwise the dev-log sender is used. Failures are logged, never thrown, so an SMS outage can't break auth/booking flows (mirrors SmtpNotificationService).
/// </summary>
public class TwilioSmsNotificationService : ISmsNotificationService
{
    private readonly TwilioOptions _options;
    private readonly IHttpClientFactory _httpFactory;

    public TwilioSmsNotificationService(TwilioOptions options, IHttpClientFactory httpFactory)
    {
        _options = options;
        _httpFactory = httpFactory;
    }

    public async Task SendSmsAsync(string phoneNumber, string message)
    {
        var sid   = _options.AccountSid;
        var token = _options.AuthToken;
        var from  = _options.NormalizedFromNumber;
        if (string.IsNullOrWhiteSpace(sid) || string.IsNullOrWhiteSpace(token) || from is null || string.IsNullOrWhiteSpace(phoneNumber))
        {
            LogProvider.Current.Information($"[SMS:skipped] To={phoneNumber}");
            return;
        }

        var to = PhoneNumberNormalizer.ToE164(phoneNumber);
        if (to is null)
        {
            LogProvider.Current.Warning($"[SMS:skipped] '{phoneNumber}' is not a valid phone number (cannot be normalised to E.164).");
            return;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.twilio.com/2010-04-01/Accounts/{sid}/Messages.json")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["To"]   = to,
                    ["From"] = from,
                    ["Body"] = message,
                }),
            };
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sid}:{token}"));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            var http = _httpFactory.CreateClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            using var resp = await http.SendAsync(req, timeout.Token);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                LogProvider.Current.Information($"TwilioSmsNotificationService->non-success {(int)resp.StatusCode}: {body}");
            }
        }
        catch (OperationCanceledException)
        {
            LogProvider.Current.Warning($"TwilioSmsNotificationService->timed out after {_options.TimeoutSeconds}s sending to {to}.");
        }
        catch (Exception e)
        {
            LogProvider.Current.Fatal(e, $"TwilioSmsNotificationService->Exception sending to {to}: {e.Message}");
        }
    }
}
