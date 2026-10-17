using System.Net;
using System.Text.RegularExpressions;

namespace Cinema.Business.Helpers;

/// <summary>Renders a Template's Subject/Body by substituting {{Token}} placeholders. Each value is
/// HTML-encoded before substitution (bodies are sent as HTML); an unknown token is left verbatim so an
/// admin editing the template can spot a typo.</summary>
public static partial class TemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex TokenPattern();

    /// <param name="encodeHtml">True for an HTML body (email); false for plain text (SMS).</param>
    public static string Render(string? text, IReadOnlyDictionary<string, string> values, bool encodeHtml = true)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return TokenPattern().Replace(text, m =>
        {
            if (!values.TryGetValue(m.Groups[1].Value, out var value))
            {
                return m.Value;
            }
            if (encodeHtml)
            {
                return WebUtility.HtmlEncode(value);
            }
            return value;
        });
    }
}
