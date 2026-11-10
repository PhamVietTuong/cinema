using System.Net.Mime;
using System.Reflection;
using Cinema.Business.Contracts;

namespace Cinema.Business.Helpers;

/// <summary>Static icons used by the booking confirmation email. Mail clients block SVG and icon fonts,
/// so each icon ships as a small PNG embedded in the assembly and attached as a CID inline image.</summary>
public static class EmailIcons
{
    private static readonly string[] Names = { "check", "clapper", "ticket", "home" };

    public static string ContentId(string name)
    {
        return $"icon-{name}@cinema";
    }

    /// <summary>The icons whose <c>cid:</c> reference appears in <paramref name="html"/>, so unused ones are not attached.</summary>
    public static List<EmailInlineImage> ReferencedBy(string html)
    {
        var images = new List<EmailInlineImage>();
        foreach (var name in Names)
        {
            var cid = ContentId(name);
            if (html.Contains($"cid:{cid}", StringComparison.Ordinal))
            {
                images.Add(new EmailInlineImage(cid, Load(name), MediaTypeNames.Image.Png, $"{name}.png"));
            }
        }
        return images;
    }

    private static byte[] Load(string name)
    {
        var assembly = typeof(EmailIcons).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith($".{name}.png", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
