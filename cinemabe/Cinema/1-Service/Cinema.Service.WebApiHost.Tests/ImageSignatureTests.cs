using System.Text;
using Cinema.Service.WebApiHost.Helpers;
using FluentAssertions;

namespace Cinema.Service.WebApiHost.Tests;

public class ImageSignatureTests
{
    private static readonly byte[] _png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
    private static readonly byte[] _jpg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 };
    private static readonly byte[] _gif = Encoding.ASCII.GetBytes("GIF89a......");
    private static readonly byte[] _webp = Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBP");

    [Theory]
    [InlineData(".png")]
    [InlineData(".PNG")]
    public void Png_IsRecognised(string ext) => ImageSignature.Matches(ext, _png).Should().BeTrue();

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    public void Jpeg_IsRecognised(string ext) => ImageSignature.Matches(ext, _jpg).Should().BeTrue();

    [Fact]
    public void Gif_IsRecognised() => ImageSignature.Matches(".gif", _gif).Should().BeTrue();

    [Fact]
    public void Webp_IsRecognised() => ImageSignature.Matches(".webp", _webp).Should().BeTrue();

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".gif")]
    [InlineData(".webp")]
    public void HtmlRenamedToAnImageExtension_IsRejected(string ext)
    {
        var html = Encoding.ASCII.GetBytes("<html><script>alert(1)</script>");

        ImageSignature.Matches(ext, html).Should().BeFalse();
    }

    [Fact]
    public void ExtensionMustMatchTheContent()
    {
        ImageSignature.Matches(".png", _jpg).Should().BeFalse();
        ImageSignature.Matches(".jpg", _png).Should().BeFalse();
        ImageSignature.Matches(".webp", _gif).Should().BeFalse();
    }

    [Fact]
    public void SvgAndUnknownExtensions_AreNeverAccepted()
    {
        var svg = Encoding.ASCII.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");

        ImageSignature.Matches(".svg", svg).Should().BeFalse();
        ImageSignature.Matches(".exe", _png).Should().BeFalse();
    }

    [Fact]
    public void TruncatedOrEmptyHeader_IsRejected()
    {
        ImageSignature.Matches(".png", new byte[] { 0x89, 0x50 }).Should().BeFalse();
        ImageSignature.Matches(".jpg", ReadOnlySpan<byte>.Empty).Should().BeFalse();
    }
}
