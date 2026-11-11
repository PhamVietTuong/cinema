using System.Net;
using System.Text.Json;
using Cinema.Business.Contracts.Payments;
using Cinema.Business.Payments;
using FluentAssertions;

namespace Cinema.Business.Tests;

public class VnPayGatewayTests
{

    private static IReadOnlyDictionary<string, string> QueryOf(string? redirectUrl)
    {
        var query = new Uri(redirectUrl!).Query.TrimStart('?');
        var result = new Dictionary<string, string>();
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = WebUtility.UrlDecode(parts[0]);
            var value = "";
            if (parts.Length > 1)
            {
                value = WebUtility.UrlDecode(parts[1]);
            }
            result[key] = value;
        }
        return result;
    }

    [Fact]
    public async Task CreatePayment_UsesClientIp()
    {
        var gateway = VnPayTestHelper.Gateway();

        var initiation = await gateway.CreatePaymentAsync(Guid.NewGuid(), 100_000, null, "203.0.113.7");

        QueryOf(initiation.RedirectUrl)["vnp_IpAddr"].Should().Be("203.0.113.7");
    }

    [Fact]
    public async Task CreatePayment_EachAttemptGetsAFreshTxnRef_ThatStillStartsWithTheInvoiceId()
    {
        // A15: VNPay rejects a reused vnp_TxnRef, so a retry after a failed payment needs a new one.
        var gateway = VnPayTestHelper.Gateway();
        var invoiceId = Guid.NewGuid();

        var first = await gateway.CreatePaymentAsync(invoiceId, 100_000, null, null);
        await Task.Delay(5);
        var second = await gateway.CreatePaymentAsync(invoiceId, 100_000, null, null);

        first.PaymentReference.Should().StartWith(invoiceId.ToString("N"));
        second.PaymentReference.Should().StartWith(invoiceId.ToString("N"));
        second.PaymentReference.Should().NotBe(first.PaymentReference);
        QueryOf(first.RedirectUrl)["vnp_TxnRef"].Should().Be(first.PaymentReference);
    }

    [Fact]
    public async Task CreatePayment_SendsGmtPlus7Timestamps_RegardlessOfServerTimeZone()
    {
        // A20: vnp_CreateDate must be Asia/Ho_Chi_Minh wall-clock; DateTime.Now would be UTC in a container.
        var gateway = VnPayTestHelper.Gateway();

        var initiation = await gateway.CreatePaymentAsync(Guid.NewGuid(), 100_000, null, null);

        var query = QueryOf(initiation.RedirectUrl);
        var created = DateTime.ParseExact(query["vnp_CreateDate"], "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var expires = DateTime.ParseExact(query["vnp_ExpireDate"], "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        created.Should().BeCloseTo(Cinema.Business.Helpers.BusinessCalendar.LocalNow(), TimeSpan.FromMinutes(1));
        (expires - created).Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void ParseCallback_AcceptsAPerAttemptTxnRef_AndReadsTheInvoiceIdFromItsPrefix()
    {
        var gateway = VnPayTestHelper.Gateway();
        var invoiceId = Guid.NewGuid();

        var result = gateway.ParseCallback(VnPayTestHelper.SignedIpn(invoiceId, 10_000_000, txnRef: invoiceId.ToString("N") + "104530123"));

        result.Status.Should().Be(PaymentCallbackStatus.Paid);
        result.InvoiceId.Should().Be(invoiceId);
    }

    [Fact]
    public void ParseCallback_TxnRefShorterThanAnInvoiceId_IsAnInvalidOrderReference()
    {
        var gateway = VnPayTestHelper.Gateway();

        var result = gateway.ParseCallback(VnPayTestHelper.SignedIpn(Guid.NewGuid(), 10_000_000, txnRef: "12345"));

        result.Status.Should().Be(PaymentCallbackStatus.InvalidOrderReference);
    }

    [Fact]
    public async Task CreatePayment_NullClientIp_FallsBackToLoopback()
    {
        var gateway = VnPayTestHelper.Gateway();

        var initiation = await gateway.CreatePaymentAsync(Guid.NewGuid(), 100_000, null, null);

        QueryOf(initiation.RedirectUrl)["vnp_IpAddr"].Should().Be("127.0.0.1");
    }

    [Fact]
    public void ParseCallback_AcceptsValidlySignedIpn()
    {
        var gateway = VnPayTestHelper.Gateway();
        var invoiceId = Guid.NewGuid();

        var callbackData = VnPayTestHelper.SignedIpn(invoiceId, 10_000_000);

        var result = gateway.ParseCallback(callbackData);

        result.Success.Should().BeTrue(result.FailureReason);
        result.InvoiceId.Should().Be(invoiceId);
    }

    [Fact]
    public void ParseCallback_ValidIpn_ExposesReceivedAmountInVnd()
    {
        var gateway = VnPayTestHelper.Gateway();
        var callbackData = VnPayTestHelper.SignedIpn(Guid.NewGuid(), 10_000_000);

        var result = gateway.ParseCallback(callbackData);

        result.Status.Should().Be(PaymentCallbackStatus.Paid);
        result.ReceivedAmount.Should().Be(100_000m);
    }

    [Fact]
    public void ParseCallback_RejectsTamperedSignature()
    {
        var gateway = VnPayTestHelper.Gateway();
        var callbackData = new Dictionary<string, string>
        {
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_TxnRef"] = Guid.NewGuid().ToString("N"),
            ["vnp_SecureHash"] = "not-a-valid-hash",
        };

        var result = gateway.ParseCallback(callbackData);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void ParseCallback_TamperedSignature_StatusInvalidSignature()
    {
        var gateway = VnPayTestHelper.Gateway();
        var callbackData = new Dictionary<string, string>
        {
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_TxnRef"] = Guid.NewGuid().ToString("N"),
            ["vnp_SecureHash"] = "not-a-valid-hash",
        };

        var result = gateway.ParseCallback(callbackData);

        result.Status.Should().Be(PaymentCallbackStatus.InvalidSignature);
    }

    [Fact]
    public void ParseCallback_FailedResponseCode_StatusPaymentFailed()
    {
        var gateway = VnPayTestHelper.Gateway();
        var callbackData = VnPayTestHelper.SignedIpn(Guid.NewGuid(), 10_000_000, responseCode: "24", transactionStatus: "02");

        var result = gateway.ParseCallback(callbackData);

        result.Status.Should().Be(PaymentCallbackStatus.PaymentFailed);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void ParseCallback_MissingAmount_StatusInvalidAmount()
    {
        var gateway = VnPayTestHelper.Gateway();
        var signed = VnPayTestHelper.SignedIpn(Guid.NewGuid(), 10_000_000, includeAmount: false);

        var result = gateway.ParseCallback(signed);

        result.Status.Should().Be(PaymentCallbackStatus.InvalidAmount);
    }

    public static IEnumerable<object[]> AllOutcomes()
    {
        foreach (PaymentCallbackOutcome outcome in Enum.GetValues(typeof(PaymentCallbackOutcome)))
        {
            yield return new object[] { outcome };
        }
    }

    [Theory]
    [MemberData(nameof(AllOutcomes))]
    public void BuildIpnResponse_MapsEveryOutcome(PaymentCallbackOutcome outcome)
    {
        var response = VnPayGateway.BuildIpnResponse(outcome);

        response.RspCode.Should().NotBeNullOrEmpty();
        response.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void BuildIpnResponse_Confirmed_SerializesWithPascalCaseKeys()
    {
        var response = VnPayGateway.BuildIpnResponse(PaymentCallbackOutcome.Confirmed);

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        json.Should().Be("""{"RspCode":"00","Message":"Confirm Success"}""");
    }
}
