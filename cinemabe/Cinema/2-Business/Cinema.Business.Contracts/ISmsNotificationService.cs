namespace Cinema.Business.Contracts;

/// <summary>
/// Sends SMS notifications. A separate channel from <see cref="INotificationService"/> (email) so SMS
/// can be sent in addition to — not instead of — email. The dev default logs; a Twilio sender plugs in
/// via configuration. Implementations must never throw (an SMS outage can't break auth/booking flows).
/// </summary>
public interface ISmsNotificationService
{
    Task SendSmsAsync(string phoneNumber, string message);
}
