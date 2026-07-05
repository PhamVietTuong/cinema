namespace Cinema.Business.Contracts;

/// <summary>
/// Sends user notifications (email/SMS). The default implementation logs to the app log for dev;
/// swap in a real SMTP/Twilio sender behind this interface via configuration.
/// </summary>
public interface INotificationService
{
    Task SendAsync(string to, string subject, string body);
}
