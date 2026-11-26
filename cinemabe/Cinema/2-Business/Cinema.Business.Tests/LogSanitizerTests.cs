using Cinema.Foundation.Logging;
using FluentAssertions;

namespace Cinema.Business.Tests;

public class LogSanitizerTests
{
    [Fact]
    public void Neutralize_CrLf_CannotStartAForgedLogLine()
    {
        var forged = "Movie 'x\r\n09:00:00 [INF] Admin logged in' not found.";

        var result = LogSanitizer.Neutralize(forged);

        result.Should().NotContain("\r").And.NotContain("\n");
        result.Should().Contain("\\r\\n");
    }

    [Fact]
    public void Neutralize_Braces_AreDoubledSoTheyAreNotParsedAsATemplate()
    {
        LogSanitizer.Neutralize("slug={Password} }").Should().Be("slug={{Password}} }}");
    }

    [Fact]
    public void Neutralize_UnicodeLineSeparatorsAndControlChars_AreRemoved()
    {
        LogSanitizer.Neutralize("a\u2028b\u0085c\u001bd").Should().Be("a b c?d");
    }

    [Fact]
    public void Neutralize_PlainTextAndTabs_AreUntouched()
    {
        LogSanitizer.Neutralize("Controller.Action->NotFound:\tok").Should().Be("Controller.Action->NotFound:\tok");
    }

    [Fact]
    public void Neutralize_NullAndEmpty_AreSafe()
    {
        LogSanitizer.Neutralize(null).Should().BeEmpty();
        LogSanitizer.Neutralize("").Should().BeEmpty();
    }
}
