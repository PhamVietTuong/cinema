using System.Net;
using System.Net.Mail;
using Cinema.Business.Contracts;
using Cinema.Foundation.Logging;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Notifications;

/// <summary>
/// Sends notifications as email over SMTP (config under "Smtp"). Registered only when
/// <c>Smtp:Host</c> is set; otherwise the dev-log sender is used. SMS can be added the
/// same way behind <see cref="INotificationService"/>. Failures are logged, never thrown,
/// so a mail outage can't break booking/auth flows.
/// </summary>
public class SmtpNotificationService : INotificationService
{
    private readonly IConfiguration _config;

    public SmtpNotificationService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        var host = _config["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(to))
        {
            LogProvider.Current.Information($"[Notification:skipped] To={to} | Subject={subject}");
            return;
        }

        var port      = int.TryParse(_config["Smtp:Port"], out var p) ? p : 587;
        var user      = _config["Smtp:User"];
        var pass      = _config["Smtp:Password"];
        var from      = _config["Smtp:From"] ?? user ?? "no-reply@cinema.vn";
        var enableSsl = !bool.TryParse(_config["Smtp:EnableSsl"], out var ssl) || ssl; // default true

        try
        {
            using var client = new SmtpClient(host, port) { EnableSsl = enableSsl };
            if (!string.IsNullOrWhiteSpace(user))
            {
                client.Credentials = new NetworkCredential(user, pass);
            }
            using var message = new MailMessage(from, to, subject, body);
            await client.SendMailAsync(message);
        }
        catch (Exception e)
        {
            LogProvider.Current.Fatal(e, $"SmtpNotificationService->Exception sending to {to}: {e.Message}");
        }
    }
}
