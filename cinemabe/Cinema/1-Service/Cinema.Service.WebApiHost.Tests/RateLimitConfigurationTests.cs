using Cinema.Service.WebApiHost.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Cinema.Service.WebApiHost.Tests;

public class RateLimitConfigurationTests
{
    private static IConfiguration Config(string? value)
    {
        var values = new Dictionary<string, string?>();
        if (value is not null)
        {
            values[RateLimitPolicies.AuthPermitLimitKey] = value;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void AuthPermitLimit_WhenNotConfigured_IsTheProductionDefaultOf10()
    {
        RateLimitPolicies.ResolveAuthPermitLimit(Config(null)).Should().Be(10);
        RateLimitPolicies.DefaultAuthPermitLimit.Should().Be(10);
    }

    [Fact]
    public void AuthPermitLimit_WhenConfigured_UsesTheConfiguredValue()
    {
        RateLimitPolicies.ResolveAuthPermitLimit(Config("1000")).Should().Be(1000);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("many")]
    [InlineData("")]
    [InlineData("2147483648")]
    public void AuthPermitLimit_WhenTheValueIsNotAPositiveInteger_FallsBackToTheDefault(string bad)
    {
        // A typo in configuration must never switch the credential-guessing limiter off.
        RateLimitPolicies.ResolveAuthPermitLimit(Config(bad)).Should().Be(10);
    }
}
