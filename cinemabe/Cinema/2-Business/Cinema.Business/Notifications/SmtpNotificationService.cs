using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Cinema.Business.Contracts;
using Cinema.Foundation.Logging;

namespace Cinema.Business.Notifications;

/// <summary>
/// Sends notifications as email over SMTP (config under "Smtp", see <see cref="SmtpOptions"/>, validated at
/// startup, with a per-send timeout). Registered only when
/// <c>Smtp:Host</c> is set; otherwise the dev-log sender is used. SMS can be added the
/// same way behind <see cref="INotificationService"/>. Failures are logged, never thrown,
/// so a mail outage can't break booking/auth flows.
/// </summary>
public class SmtpNotificationService : INotificationService
{
    private readonly SmtpOptions _options;

    public SmtpNotificationService(SmtpOptions options)
    {
        _options = options;
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
        var host = _options.Host;
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(to))
        {
            LogProvider.Current.Information($"[Notification:skipped] To={to} | Subject={subject}");
            return;
        }

        var port      = _options.Port;
        var user      = _options.User;
        var pass      = _options.Password;
        var from      = string.IsNullOrWhiteSpace(_options.From) ? user ?? "no-reply@cinema.vn" : _options.From;
        var enableSsl = _options.EnableSsl;

        try
        {
            using var client = new SmtpClient(host, port) { EnableSsl = enableSsl, Timeout = _options.TimeoutSeconds * 1000 };
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
