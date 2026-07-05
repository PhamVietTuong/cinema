using Cinema.Business.Contracts;
using Cinema.Foundation.Logging;

namespace Cinema.Business.Notifications;

/// <summary>
/// Development notification sender — writes the message to the app log instead of sending it.
/// Lets password-reset and booking-confirmation flows run end-to-end without SMTP/SMS credentials.
/// Replace with a real SMTP/Twilio implementation (same interface) for production.
/// </summary>
public class DevLogNotificationService : INotificationService
{
    public Task SendAsync(string to, string subject, string body)
    {
        LogProvider.Current.Information($"[Notification] To={to} | Subject={subject}\n{body}");
        return Task.CompletedTask;
    }
}
