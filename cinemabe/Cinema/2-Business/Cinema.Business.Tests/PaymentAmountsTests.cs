using System.Net;
using Cinema.Business.Helpers;
using FluentAssertions;

namespace Cinema.Business.Tests;

[Trait("Req", "BR-PAY-02")]
public class PaymentAmountsTests
{
    [Theory]
    [InlineData(100_000, 100_000)]
    [InlineData(100_000.4, 100_000)]
    [InlineData(100_000.6, 100_001)]
    // Round-to-even at exactly .5: this is what customers are charged today, so a change here must be deliberate.
    [InlineData(100_000.5, 100_000)]
    [InlineData(100_001.5, 100_002)]
    public void GatewayAmount_RoundsToWholeDong_PinnedModeIsToEven(double total, long expected)
    {
        PaymentAmounts.GatewayAmount(total).Should().Be(expected);
    }

    [Theory]
    [InlineData(100_000.0)]
    [InlineData(45_500.5)]
    [InlineData(99_999.4)]
    public async Task VnpAmountSentToTheGateway_IsTheSameAmountTheCallbackIsCheckedAgainst(double invoiceTotal)
    {
        var gateway = VnPayTestHelper.Gateway();

        var initiation = await gateway.CreatePaymentAsync(Guid.NewGuid(), invoiceTotal, null, null);

        var query = new Uri(initiation.RedirectUrl!).Query.TrimStart('?')
            .Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => WebUtility.UrlDecode(p[0]), p => WebUtility.UrlDecode(p[1]));
        long.Parse(query["vnp_Amount"]).Should().Be(PaymentAmounts.GatewayAmount(invoiceTotal) * 100);
    }
}
