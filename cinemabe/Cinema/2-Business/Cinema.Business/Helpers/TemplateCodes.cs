namespace Cinema.Business.Helpers;

/// <summary>Template Codes looked up by the sending code. Must match the seeded rows in
/// create_db.sql / upgrade_db.sql.</summary>
public static class TemplateCodes
{
    public const string BookingConfirmationEmail = "BookingConfirmationEmail";
    public const string BookingConfirmationSms = "BookingConfirmationSms";
}
