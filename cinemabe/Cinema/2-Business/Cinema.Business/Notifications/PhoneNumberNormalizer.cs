using System.Text.RegularExpressions;

namespace Cinema.Business.Notifications;

/// <summary>
/// Normalises user-typed phone numbers to E.164 (<c>+84900123456</c>), the only format Twilio accepts.
/// A national number with a leading trunk <c>0</c> (<c>0900123456</c>) gets the default country code, which
/// is Vietnam (84) because that is where the cinemas are. Returns null for anything that cannot be a valid
/// E.164 number, so the caller can log and skip instead of sending a request Twilio will reject.
/// </summary>
public static partial class PhoneNumberNormalizer
{
    public const string DefaultCountryCode = "84";

    [GeneratedRegex(@"^\+[1-9]\d{6,14}$")]
    private static partial Regex E164Pattern();

    /// <summary>True when the value is already a well-formed E.164 number.</summary>
    public static bool IsE164(string? value)
    {
        return !string.IsNullOrEmpty(value) && E164Pattern().IsMatch(value);
    }

    public static string? ToE164(string? raw, string defaultCountryCode = DefaultCountryCode)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        var hasPlus = trimmed.StartsWith('+');
        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }
        // Only separators may sit between digits; letters or other symbols mean this is not a phone number.
        if (trimmed.Any(c => !char.IsAsciiDigit(c) && !" -.()+".Contains(c)))
        {
            return null;
        }

        string candidate;
        if (hasPlus)
        {
            candidate = "+" + digits;
        }
        else if (digits.StartsWith("00"))
        {
            candidate = "+" + digits[2..];
        }
        else if (digits.StartsWith('0'))
        {
            candidate = "+" + defaultCountryCode + digits[1..];
        }
        else if (digits.StartsWith(defaultCountryCode) && digits.Length > defaultCountryCode.Length + 7)
        {
            candidate = "+" + digits;
        }
        else
        {
            return null;
        }

        return IsE164(candidate) ? candidate : null;
    }
}
