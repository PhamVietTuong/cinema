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
    /// <param name="rawKeys">Keys whose value is inserted verbatim, bypassing <paramref name="encodeHtml"/>.
    /// Only for HTML built server-side from trusted data (e.g. a generated &lt;img&gt; fragment) — the caller
    /// must encode any user-supplied data inside that HTML itself.</param>
    public static string Render(string? text, IReadOnlyDictionary<string, string> values, bool encodeHtml = true, IReadOnlySet<string>? rawKeys = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return TokenPattern().Replace(text, m =>
        {
            var key = m.Groups[1].Value;
            if (!values.TryGetValue(key, out var value))
            {
                return m.Value;
            }
            if (rawKeys is not null && rawKeys.Contains(key))
            {
                return value;
            }
            if (encodeHtml)
            {
                return WebUtility.HtmlEncode(value);
            }
            return value;
        });
    }
}
