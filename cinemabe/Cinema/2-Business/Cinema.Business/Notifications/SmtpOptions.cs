using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Notifications;

/// <summary>Typed view of the "Smtp" config section, validated once at startup instead of on every send.</summary>
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? From { get; set; }
    public bool EnableSsl { get; set; } = true;
    /// <summary>Hard cap on one send; the default SmtpClient timeout is 100 s, long enough to stall a booking request.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>True when email delivery is switched on (a host is configured).</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);

    public static SmtpOptions Load(IConfiguration config)
    {
        var section = config.GetSection(SectionName);
        return new SmtpOptions
        {
            Host           = section["Host"],
            Port           = int.TryParse(section["Port"], out var port) ? port : 587,
            User           = section["User"],
            Password       = section["Password"],
            From           = section["From"],
            EnableSsl      = !bool.TryParse(section["EnableSsl"], out var ssl) || ssl,
            TimeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var timeout) ? timeout : 15,
        };
    }

    /// <summary>Problems that would make every send fail. Empty when email is unconfigured (dev-log fallback) or valid.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (!IsConfigured)
        {
            // A section with credentials/sender but no host would silently fall back to the dev-log sender.
            if (!string.IsNullOrWhiteSpace(User) || !string.IsNullOrWhiteSpace(Password) || !string.IsNullOrWhiteSpace(From))
            {
                errors.Add($"{SectionName}:Host is required when {SectionName}:User, Password or From is set.");
            }
            return errors;
        }
        if (Port is < 1 or > 65535)
        {
            errors.Add($"{SectionName}:Port must be 1-65535 (was {Port}).");
        }
        if (TimeoutSeconds is < 1 or > 120)
        {
            errors.Add($"{SectionName}:TimeoutSeconds must be 1-120 (was {TimeoutSeconds}).");
        }
        var sender = string.IsNullOrWhiteSpace(From) ? User : From;
        if (string.IsNullOrWhiteSpace(sender) || !IsEmail(sender))
        {
            errors.Add($"{SectionName}:From (or {SectionName}:User) must be a valid sender email address.");
        }
        if (!string.IsNullOrWhiteSpace(User) && string.IsNullOrWhiteSpace(Password))
        {
            errors.Add($"{SectionName}:Password is required when {SectionName}:User is set.");
        }
        return errors;
    }

    private static bool IsEmail(string value)
    {
        // Anything MailMessage accepts, including a display name ("Cinema <noreply@x.vn>").
        return MailAddress.TryCreate(value, out _);
    }
}
