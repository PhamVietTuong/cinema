using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Notifications;

/// <summary>Typed view of the "Sms:Twilio" config section, validated once at startup.</summary>
public class TwilioOptions
{
    public const string SectionName = "Sms:Twilio";

    public string? AccountSid { get; set; }
    public string? AuthToken { get; set; }
    public string? FromNumber { get; set; }
    /// <summary>Hard cap on one Twilio request, so an outage cannot hold up login or booking.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>True when SMS delivery is switched on (an account SID is configured).</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(AccountSid);

    public static TwilioOptions Load(IConfiguration config)
    {
        var section = config.GetSection(SectionName);
        return new TwilioOptions
        {
            AccountSid     = section["AccountSid"],
            AuthToken      = section["AuthToken"],
            FromNumber     = section["FromNumber"],
            TimeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var timeout) ? timeout : 10,
        };
    }

    /// <summary>The sender number in E.164, or null when it is missing or malformed.</summary>
    public string? NormalizedFromNumber => PhoneNumberNormalizer.ToE164(FromNumber);

    /// <summary>Problems that would make every send fail. Empty when SMS is unconfigured (dev-log fallback) or valid.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (!IsConfigured)
        {
            return errors;
        }
        if (!AccountSid!.StartsWith("AC", StringComparison.Ordinal))
        {
            errors.Add($"{SectionName}:AccountSid must start with 'AC'.");
        }
        if (string.IsNullOrWhiteSpace(AuthToken))
        {
            errors.Add($"{SectionName}:AuthToken is required when {SectionName}:AccountSid is set.");
        }
        if (NormalizedFromNumber is null)
        {
            errors.Add($"{SectionName}:FromNumber must be a valid phone number (E.164, e.g. +15005550006).");
        }
        if (TimeoutSeconds is < 1 or > 120)
        {
            errors.Add($"{SectionName}:TimeoutSeconds must be 1-120 (was {TimeoutSeconds}).");
        }
        return errors;
    }
}
