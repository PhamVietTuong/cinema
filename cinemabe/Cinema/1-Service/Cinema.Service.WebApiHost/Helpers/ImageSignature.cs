namespace Cinema.Service.WebApiHost.Helpers;

/// <summary>
/// Checks the leading bytes of an uploaded file against the format its extension claims. The extension and the
/// Content-Type header are both chosen by the client, so on their own they let a non-image (a script or HTML
/// page renamed to .png) be stored under the site's own origin.
/// </summary>
public static class ImageSignature
{
    /// <summary>Bytes of the file's start that <see cref="Matches"/> needs.</summary>
    public const int HeaderLength = 12;

    public static bool Matches(string extension, ReadOnlySpan<byte> header)
    {
        switch (extension.ToLowerInvariant())
        {
            case ".png":
                return header.Length >= 8
                    && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                    && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;

            case ".jpg":
            case ".jpeg":
                return header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

            case ".gif":
                // GIF87a / GIF89a
                return header.Length >= 6
                    && header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'8'
                    && (header[4] == (byte)'7' || header[4] == (byte)'9') && header[5] == (byte)'a';

            case ".webp":
                // RIFF....WEBP
                return header.Length >= 12
                    && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
                    && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P';

            default:
                return false;
        }
    }
}
