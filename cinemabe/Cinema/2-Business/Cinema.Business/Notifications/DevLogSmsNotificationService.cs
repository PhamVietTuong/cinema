using Cinema.Business.Contracts;
using Cinema.Foundation.Logging;

namespace Cinema.Business.Notifications;

/// <summary>
/// Development SMS sender — writes the message to the app log instead of sending it, so 2FA and
/// booking-confirmation flows run end-to-end without Twilio credentials. Swap in
/// <see cref="TwilioSmsNotificationService"/> via configuration for production.
/// </summary>
public class DevLogSmsNotificationService : ISmsNotificationService
{
    public Task SendSmsAsync(string phoneNumber, string message)
    {
        LogProvider.Current.Information($"[SMS] To={phoneNumber}\n{message}");
        return Task.CompletedTask;
    }
}
