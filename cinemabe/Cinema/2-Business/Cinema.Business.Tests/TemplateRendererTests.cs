using Cinema.Business.Helpers;
using FluentAssertions;

namespace Cinema.Business.Tests;

public class TemplateRendererTests
{
    [Fact]
    public void Render_ReplacesKnownTokens()
    {
        var result = TemplateRenderer.Render("Hello {{Name}}, code {{Code}}",
            new Dictionary<string, string> { ["Name"] = "An", ["Code"] = "ABC" });

        result.Should().Be("Hello An, code ABC");
    }

    [Fact]
    public void Render_TreatsWhitespaceInsideBracesAsTolerable()
    {
        var result = TemplateRenderer.Render("Hello {{ Name }}", new Dictionary<string, string> { ["Name"] = "An" });

        result.Should().Be("Hello An");
    }

    [Fact]
    public void Render_LeavesUnknownTokenVerbatim()
    {
        var result = TemplateRenderer.Render("Hello {{Unknown}}", new Dictionary<string, string>());

        result.Should().Be("Hello {{Unknown}}");
    }

    [Fact]
    public void Render_NullText_ReturnsEmpty()
    {
        TemplateRenderer.Render(null, new Dictionary<string, string>()).Should().BeEmpty();
    }

    [Fact]
    public void Render_HtmlEncodesValuesByDefault()
    {
        var result = TemplateRenderer.Render("{{Name}}", new Dictionary<string, string> { ["Name"] = "A & B" });

        result.Should().Be("A &amp; B");
    }

    [Fact]
    public void Render_PlainText_DoesNotEncodeWhenDisabled()
    {
        var result = TemplateRenderer.Render("{{Name}}", new Dictionary<string, string> { ["Name"] = "A & B" }, encodeHtml: false);

        result.Should().Be("A & B");
    }
}
