using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Cinema.Business.Helpers;

/// <summary>Turns a movie title into a URL-safe slug and picks a unique one against already-taken slugs.
/// Mirrors the retired frontend `slugify()` util so existing links keep the same shape.</summary>
public static class SlugHelper
{
    public const int MaxBaseLength = 300;
    public const string Fallback = "movie";

    private static readonly Regex NonAlphaNumeric = new("[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex TrimDashes = new("(^-+)|(-+$)", RegexOptions.Compiled);

    public static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        var lowered = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var dashed = NonAlphaNumeric.Replace(lowered, "-");
        return TrimDashes.Replace(dashed, string.Empty);
    }

    public static string ToBaseSlug(string? title)
    {
        var slug = Slugify(title);
        if (slug.Length > MaxBaseLength)
        {
            slug = TrimDashes.Replace(slug[..MaxBaseLength], string.Empty);
        }

        if (slug.Length == 0)
        {
            return Fallback;
        }

        if (Guid.TryParse(slug, out _))
        {
            return $"{Fallback}-{slug}";
        }

        return slug;
    }

    public static string PickUnique(string baseSlug, IReadOnlyCollection<string> taken)
    {
        var takenSet = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        if (!takenSet.Contains(baseSlug))
        {
            return baseSlug;
        }

        var n = 2;
        while (takenSet.Contains($"{baseSlug}-{n}"))
        {
            n++;
        }

        return $"{baseSlug}-{n}";
    }
}
