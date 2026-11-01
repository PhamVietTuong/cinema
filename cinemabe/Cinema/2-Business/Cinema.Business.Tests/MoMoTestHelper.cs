using System.Security.Cryptography;
using System.Text;
using Cinema.Business.Payments;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Cinema.Business.Tests;

/// <summary>Shared MoMo sandbox config + IPN-signing helpers, reused across MoMo test classes.</summary>
internal static class MoMoTestHelper
{
    public const string PartnerCode = "MOMOTEST";
    public const string AccessKey = "TESTACCESS";
    public const string SecretKey = "TESTSECRET";
    public const string Endpoint = "https://test-payment.momo.vn/v2/gateway/api/create";

    public static MoMoGateway Gateway(IHttpClientFactory? httpFactory = null, IDictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Payments:MoMo:PartnerCode"] = PartnerCode,
            ["Payments:MoMo:AccessKey"] = AccessKey,
            ["Payments:MoMo:SecretKey"] = SecretKey,
            ["Payments:MoMo:Endpoint"] = Endpoint,
            ["Payments:MoMo:ReturnUrl"] = "https://cinema.example/payment-return",
            ["Payments:MoMo:IpnUrl"] = "https://cinema.example/api/Payment/PaymentCallback?provider=MoMo",
        };
        if (overrides != null)
        {
            foreach (var kv in overrides)
            {
                values[kv.Key] = kv.Value;
            }
        }
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new MoMoGateway(config, httpFactory ?? Mock.Of<IHttpClientFactory>());
    }

    /// <summary>Builds a validly-signed MoMo IPN field set, simulating MoMo's own callback. The signature is
    /// computed independently from the field order MoMo documents (not by reusing the gateway's own private
    /// signing method), so a test that checks it actually proves the gateway verifies correctly. Pass
    /// <paramref name="includeAmount"/> = false to sign a callback that never had an amount field at all
    /// (removing it from an already-signed set would just make the signature invalid instead).</summary>
    public static Dictionary<string, string> SignedIpn(Guid invoiceId, long amount, string resultCode = "0", bool includeAmount = true, string? orderIdOverride = null)
    {
        var orderId = orderIdOverride ?? invoiceId.ToString("N");
        var fields = new Dictionary<string, string>
        {
            ["partnerCode"] = PartnerCode,
            ["orderId"] = orderId,
            ["requestId"] = Guid.NewGuid().ToString("N"),
            ["orderInfo"] = $"Thanh toan don hang {orderId}",
            ["orderType"] = "momo_wallet",
            ["transId"] = "4088878653",
            ["resultCode"] = resultCode,
            ["message"] = resultCode == "0" ? "Successful." : "Failed.",
            ["payType"] = "qr",
            ["responseTime"] = "1700000000000",
            ["extraData"] = "",
        };
        if (includeAmount)
        {
            fields["amount"] = amount.ToString();
        }

        string G(string k) => fields.TryGetValue(k, out var v) ? v : "";
        var raw = $"accessKey={AccessKey}&amount={G("amount")}&extraData={G("extraData")}&message={G("message")}" +
                  $"&orderId={G("orderId")}&orderInfo={G("orderInfo")}&orderType={G("orderType")}" +
                  $"&partnerCode={G("partnerCode")}&payType={G("payType")}&requestId={G("requestId")}" +
                  $"&responseTime={G("responseTime")}&resultCode={G("resultCode")}&transId={G("transId")}";

        fields["signature"] = Sign(raw);
        return fields;
    }

    public static string Sign(string raw)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SecretKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}
