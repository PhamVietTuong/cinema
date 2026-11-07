using System.Net;
using Cinema.Business.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Tests;

public class NotificationTests
{
    [Theory]
    [InlineData("0900123456", "+84900123456")]
    [InlineData("090 012 3456", "+84900123456")]
    [InlineData("0900-123-456", "+84900123456")]
    [InlineData("(090) 012.3456", "+84900123456")]
    [InlineData("+84900123456", "+84900123456")]
    [InlineData("+84 900 123 456", "+84900123456")]
    [InlineData("84900123456", "+84900123456")]
    [InlineData("0084900123456", "+84900123456")]
    [InlineData("+15005550006", "+15005550006")]
    [InlineData("+84 0900123456", "+84900123456")]
    [InlineData("0084 0900123456", "+84900123456")]
    public void ToE164_NormalisesValidNumbers(string raw, string expected)
    {
        PhoneNumberNormalizer.ToE164(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("090012x456")]
    [InlineData("123")]
    [InlineData("+0123456789")]
    [InlineData("0")]
    [InlineData("+84")]
    [InlineData("9001234")]
    public void ToE164_RejectsInvalidNumbers(string? raw)
    {
        PhoneNumberNormalizer.ToE164(raw).Should().BeNull();
    }

    [Fact]
    public void SmtpOptions_Unconfigured_IsValidAndInactive()
    {
        var options = SmtpOptions.Load(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "",
            ["Smtp:Port"] = "587",
        }).Build());

        options.IsConfigured.Should().BeFalse();
        options.Validate().Should().BeEmpty();
    }

    [Fact]
    public void SmtpOptions_Valid_HasNoErrors_AndDefaults()
    {
        var options = SmtpOptions.Load(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.com",
            ["Smtp:User"] = "noreply@example.com",
            ["Smtp:Password"] = "secret",
            ["Smtp:From"] = "noreply@example.com",
        }).Build());

        options.IsConfigured.Should().BeTrue();
        options.Port.Should().Be(587);
        options.EnableSsl.Should().BeTrue();
        options.TimeoutSeconds.Should().Be(15);
        options.Validate().Should().BeEmpty();
    }

    [Fact]
    public void SmtpOptions_Misconfigured_ReportsEveryProblem()
    {
        var options = new SmtpOptions { Host = "smtp.example.com", Port = 70000, User = "u", Password = null, From = "not-an-email", TimeoutSeconds = 0 };

        var errors = options.Validate();

        errors.Should().HaveCount(4);
        errors.Should().Contain(e => e.Contains("Port"));
        errors.Should().Contain(e => e.Contains("TimeoutSeconds"));
        errors.Should().Contain(e => e.Contains("From"));
        errors.Should().Contain(e => e.Contains("Password"));
    }

    [Theory]
    [InlineData("Cinema <noreply@example.com>")]
    [InlineData("\"Cinema VN\" <noreply@example.com>")]
    [InlineData("noreply@example.com")]
    public void SmtpOptions_FromWithDisplayName_IsAccepted(string from)
    {
        var options = new SmtpOptions { Host = "smtp.example.com", From = from };

        options.Validate().Should().BeEmpty();
    }

    [Theory]
    [InlineData("user", null, null)]
    [InlineData(null, "secret", null)]
    [InlineData(null, null, "noreply@example.com")]
    public void SmtpOptions_StrayFieldsWithoutHost_Fail(string? user, string? password, string? from)
    {
        var options = new SmtpOptions { Host = "", User = user, Password = password, From = from };

        options.Validate().Should().ContainSingle().Which.Should().Contain("Host");
    }

    [Theory]
    [InlineData("token", null)]
    [InlineData(null, "+15005550006")]
    public void TwilioOptions_StrayFieldsWithoutSid_Fail(string? token, string? from)
    {
        var options = new TwilioOptions { AccountSid = "", AuthToken = token, FromNumber = from };

        options.Validate().Should().ContainSingle().Which.Should().Contain("AccountSid");
    }

    [Fact]
    public void TwilioOptions_Unconfigured_IsValidAndInactive()
    {
        var options = new TwilioOptions();

        options.IsConfigured.Should().BeFalse();
        options.Validate().Should().BeEmpty();
    }

    [Fact]
    public void TwilioOptions_PartiallyConfigured_ReportsMissingParts()
    {
        var options = new TwilioOptions { AccountSid = "ACxxxxxxxx", AuthToken = "", FromNumber = "nope" };

        var errors = options.Validate();

        errors.Should().Contain(e => e.Contains("AuthToken"));
        errors.Should().Contain(e => e.Contains("FromNumber"));
    }

    [Fact]
    public void TwilioOptions_Valid_NormalisesFromNumber()
    {
        var options = new TwilioOptions { AccountSid = "ACxxxxxxxx", AuthToken = "token", FromNumber = "0900123456" };

        options.Validate().Should().BeEmpty();
        options.NormalizedFromNumber.Should().Be("+84900123456");
    }

    [Fact]
    public async Task Twilio_SendsNormalisedRecipient()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Created));
        var sut = NewTwilio(handler);

        await sut.SendSmsAsync("0900 123 456", "hello");

        handler.Requests.Should().HaveCount(1);
        handler.Bodies.Single().Should().Contain("To=%2B84900123456");
        handler.Bodies.Single().Should().Contain("From=%2B15005550006");
    }

    [Fact]
    public async Task Twilio_InvalidRecipient_IsSkipped_WithoutThrowing()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Created));
        var sut = NewTwilio(handler);

        await sut.Invoking(s => s.SendSmsAsync("not a number", "hello")).Should().NotThrowAsync();

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Twilio_Timeout_IsSwallowed()
    {
        var handler = new RecordingHandler(_ => throw new TaskCanceledException("simulated timeout"));
        var sut = NewTwilio(handler);

        await sut.Invoking(s => s.SendSmsAsync("0900123456", "hello")).Should().NotThrowAsync();
    }

    [Fact]
    public async Task Twilio_SlowServer_IsCutOffByTimeout()
    {
        var handler = new SlowHandler();
        var options = new TwilioOptions { AccountSid = "ACxxxxxxxx", AuthToken = "token", FromNumber = "+15005550006", TimeoutSeconds = 1 };
        var sut = new TwilioSmsNotificationService(options, new FakeFactory(handler));

        var started = DateTime.UtcNow;
        await sut.SendSmsAsync("0900123456", "hello");

        (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(8));
        handler.WasCancelled.Should().BeTrue();
    }

    private static TwilioSmsNotificationService NewTwilio(HttpMessageHandler handler)
    {
        var options = new TwilioOptions { AccountSid = "ACxxxxxxxx", AuthToken = "token", FromNumber = "+15005550006" };
        return new TwilioSmsNotificationService(options, new FakeFactory(handler));
    }

    private sealed class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> Bodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return respond(request);
        }
    }

    private sealed class SlowHandler : HttpMessageHandler
    {
        public bool WasCancelled { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }
            return new HttpResponseMessage(HttpStatusCode.Created);
        }
    }
}
