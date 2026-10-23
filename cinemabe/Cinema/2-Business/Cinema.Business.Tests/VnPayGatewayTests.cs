using System.Net;
using System.Security.Cryptography;
using System.Text;
using Cinema.Business.Payments;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Tests;

public class VnPayGatewayTests
{
    private static VnPayGateway Gateway()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payments:VnPay:TmnCode"] = "TESTCODE",
                ["Payments:VnPay:HashSecret"] = "TESTSECRET",
                ["Payments:VnPay:PayUrl"] = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
            })
            .Build();
        return new VnPayGateway(config);
    }

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
        var gateway = Gateway();

        var initiation = await gateway.CreatePaymentAsync(Guid.NewGuid(), 100_000, null, "203.0.113.7");

        QueryOf(initiation.RedirectUrl)["vnp_IpAddr"].Should().Be("203.0.113.7");
    }

    [Fact]
    public async Task CreatePayment_NullClientIp_FallsBackToLoopback()
    {
        var gateway = Gateway();

        var initiation = await gateway.CreatePaymentAsync(Guid.NewGuid(), 100_000, null, null);

        QueryOf(initiation.RedirectUrl)["vnp_IpAddr"].Should().Be("127.0.0.1");
    }

    [Fact]
    public void ParseCallback_AcceptsValidlySignedIpn()
    {
        var gateway = Gateway();
        var invoiceId = Guid.NewGuid();

        // Simulates VNPay's own IPN: its own param set, signed with the shared secret — not a reuse
        // of the create-request's hash, which covers a different set of fields.
        var p = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = "10000000",
            ["vnp_BankCode"] = "NCB",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionNo"] = "13456789",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_TxnRef"] = invoiceId.ToString("N"),
        };
        var signData = string.Join("&", p.Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes("TESTSECRET"));
        var secureHash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signData))).ToLowerInvariant();

        var callbackData = new Dictionary<string, string>(p) { ["vnp_SecureHash"] = secureHash };

        var result = gateway.ParseCallback(callbackData);

        result.Success.Should().BeTrue(result.FailureReason);
        result.InvoiceId.Should().Be(invoiceId);
    }

    [Fact]
    public void ParseCallback_RejectsTamperedSignature()
    {
        var gateway = Gateway();
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
}
