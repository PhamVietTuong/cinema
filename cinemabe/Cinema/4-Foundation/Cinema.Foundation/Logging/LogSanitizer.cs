using System.Text;

namespace Cinema.Foundation.Logging;

/// <summary>
/// Makes a caller-built log string safe to hand to Serilog as a message template. Callers interpolate request
/// values (a slug, an exception message that embeds one) into the string, so an attacker could otherwise inject
/// CR/LF to forge extra log lines, or braces to be parsed as a template. Control characters become visible
/// escapes and braces are doubled so the text is always logged literally.
/// </summary>
public static class LogSanitizer
{
    public static string Neutralize(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message ?? string.Empty;
        }

        StringBuilder? sb = null;
        for (var i = 0; i < message.Length; i++)
        {
            var c = message[i];
            string? replacement = c switch
            {
                '\r' => "\\r",
                '\n' => "\\n",
                '\u0085' or '\u2028' or '\u2029' => " ",
                '{' => "{{",
                '}' => "}}",
                _ when char.IsControl(c) && c != '\t' => "?",
                _ => null,
            };

            if (replacement == null)
            {
                sb?.Append(c);
                continue;
            }

            sb ??= new StringBuilder(message.Length + 8).Append(message, 0, i);
            sb.Append(replacement);
        }

        return sb?.ToString() ?? message;
    }
}
