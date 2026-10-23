using QRCoder;

namespace Cinema.Business.Helpers;

/// <summary>Renders text as a QR code PNG, matching the error-correction level the frontend's
/// `qrcode` package defaults to. Uses QRCoder's managed PNG renderer so it needs no OS-specific
/// graphics stack.</summary>
public static class QrCodeImage
{
    public static byte[] ToPng(string text, int pixelsPerModule = 6)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(pixelsPerModule);
    }
}
