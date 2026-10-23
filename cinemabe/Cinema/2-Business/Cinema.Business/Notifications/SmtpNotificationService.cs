using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
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

    public Task SendAsync(string to, string subject, string body)
    {
        return SendCoreAsync(to, subject, body, inlineImages: null);
    }

    public Task SendAsync(string to, string subject, string htmlBody, IReadOnlyList<EmailInlineImage> inlineImages)
    {
        return SendCoreAsync(to, subject, htmlBody, inlineImages);
    }

    private async Task SendCoreAsync(string to, string subject, string body, IReadOnlyList<EmailInlineImage>? inlineImages)
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

            using var message = new MailMessage(from, to, subject, body: null) { IsBodyHtml = true };

            if (inlineImages is { Count: > 0 })
            {
                var view = AlternateView.CreateAlternateViewFromString(body, Encoding.UTF8, MediaTypeNames.Text.Html);
                foreach (var image in inlineImages)
                {
                    var resource = new LinkedResource(new MemoryStream(image.Content), image.MediaType)
                    {
                        ContentId = image.ContentId,
                        TransferEncoding = TransferEncoding.Base64,
                        ContentType = { Name = image.FileName },
                    };
                    view.LinkedResources.Add(resource);
                }
                message.AlternateViews.Add(view);
            }
            else
            {
                message.Body = body;
            }

            await client.SendMailAsync(message);
            LogProvider.Current.Information($"[Notification:sent] To={to} | Subject={subject}");
        }
        catch (Exception e)
        {
            LogProvider.Current.Fatal(e, $"SmtpNotificationService->Exception sending to {to}: {e.Message}");
        }
    }
}
