using System.Net;
using System.Security.Cryptography;
using System.Text;
using Cinema.Business.Payments;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Tests;

/// <summary>Shared VNPay sandbox config + IPN-signing helpers, reused across VNPay test classes.</summary>
internal static class VnPayTestHelper
{
    public const string Secret = "TESTSECRET";

    public static VnPayGateway Gateway()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payments:VnPay:TmnCode"] = "TESTCODE",
                ["Payments:VnPay:HashSecret"] = Secret,
                ["Payments:VnPay:PayUrl"] = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
            })
            .Build();
        return new VnPayGateway(config);
    }

    /// <summary>Builds a validly-signed IPN parameter set, simulating VNPay's own callback (its own param
    /// set, signed with the shared secret — not a reuse of the create-request's hash). Pass
    /// <paramref name="includeAmount"/> = false to sign a callback that never had vnp_Amount at all
    /// (removing it from an already-signed set would just make the signature invalid instead).</summary>
    public static Dictionary<string, string> SignedIpn(Guid invoiceId, long vnpAmount, string responseCode = "00", string transactionStatus = "00", bool includeAmount = true)
    {
        var p = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_BankCode"] = "NCB",
            ["vnp_ResponseCode"] = responseCode,
            ["vnp_TransactionNo"] = "13456789",
            ["vnp_TransactionStatus"] = transactionStatus,
            ["vnp_TxnRef"] = invoiceId.ToString("N"),
        };
        if (includeAmount)
        {
            p["vnp_Amount"] = vnpAmount.ToString();
        }
        var signData = string.Join("&", p.Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(Secret));
        var secureHash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signData))).ToLowerInvariant();

        return new Dictionary<string, string>(p) { ["vnp_SecureHash"] = secureHash };
    }
}
