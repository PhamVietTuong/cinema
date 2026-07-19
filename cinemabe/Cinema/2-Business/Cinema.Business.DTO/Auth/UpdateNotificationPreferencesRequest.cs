namespace Cinema.Business.DTO.Auth;

/// <summary>Customer's email notification opt-in/out settings.</summary>
public class UpdateNotificationPreferencesRequest
{
    public bool NotifyBookingEmails { get; set; }
    public bool NotifyPromotionEmails { get; set; }
    public bool NotifyReminderEmails { get; set; }
}
