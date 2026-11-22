using Cinema.Business.Managers.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Tests;

[Trait("Req", "BR-AUTH-02")]
public class FacebookTokenValidatorTests
{
    private static FacebookTokenValidator Build(Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new FacebookTokenValidator(config);
    }

    [Fact]
    public async Task Validate_AppSecretMissing_FailsClosedInsteadOfSkippingTheAudienceCheck()
    {
        var sut = Build(new() { ["Facebook:AppId"] = "123" });

        await sut.Invoking(s => s.ValidateAsync("any-token"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*AppSecret*");
    }

    [Fact]
    public async Task Validate_AppSecretBlank_FailsClosed()
    {
        var sut = Build(new() { ["Facebook:AppId"] = "123", ["Facebook:AppSecret"] = "  " });

        await sut.Invoking(s => s.ValidateAsync("any-token"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*AppSecret*");
    }

    [Fact]
    public async Task Validate_AppIdMissing_Throws()
    {
        var sut = Build(new() { ["Facebook:AppSecret"] = "s" });

        await sut.Invoking(s => s.ValidateAsync("any-token"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*AppId*");
    }

    [Fact]
    public async Task Validate_EmptyToken_ReturnsNull()
    {
        var sut = Build(new() { ["Facebook:AppId"] = "123", ["Facebook:AppSecret"] = "s" });

        (await sut.ValidateAsync("")).Should().BeNull();
    }
}
