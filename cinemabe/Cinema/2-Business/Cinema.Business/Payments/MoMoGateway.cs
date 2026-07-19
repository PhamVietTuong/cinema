using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cinema.Business.Contracts.Payments;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Payments;

/// <summary>
/// MoMo e-wallet gateway (Vietnam), v2 "captureWallet" flow. We POST a signed create-request and MoMo
/// returns a <c>payUrl</c> to redirect the user to. MoMo then calls our IPN endpoint with a signed
/// payload — <see cref="ParseCallback"/> re-verifies that HMAC-SHA256 signature and <c>resultCode == 0</c>.
///
/// Config under <c>Payments:MoMo</c>: <c>PartnerCode</c>, <c>AccessKey</c>, <c>SecretKey</c>,
/// <c>Endpoint</c>, <c>ReturnUrl</c>, <c>IpnUrl</c>.
/// </summary>
public class MoMoGateway : IPaymentGateway
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;

    public MoMoGateway(IConfiguration config, IHttpClientFactory httpFactory)
    {
        _config = config;
        _httpFactory = httpFactory;
    }

    public string Name => "MoMo";

    private string PartnerCode => _config["Payments:MoMo:PartnerCode"] ?? "";
    private string AccessKey   => _config["Payments:MoMo:AccessKey"]   ?? "";
    private string SecretKey   => _config["Payments:MoMo:SecretKey"]   ?? "";
    private string Endpoint    => _config["Payments:MoMo:Endpoint"]    ?? "https://test-payment.momo.vn/v2/gateway/api/create";

    public async Task<PaymentInitiation> CreatePaymentAsync(Guid invoiceId, double amount, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(PartnerCode) || string.IsNullOrWhiteSpace(AccessKey) || string.IsNullOrWhiteSpace(SecretKey))
        {
            throw new InvalidOperationException("MoMo is not configured (Payments:MoMo:PartnerCode / AccessKey / SecretKey).");
        }

        var orderId   = invoiceId.ToString("N");
        var requestId = Guid.NewGuid().ToString("N");
        var amt       = ((long)Math.Round(amount)).ToString();
        var orderInfo = $"Thanh toan don hang {orderId}";
        var redirect  = returnUrl ?? _config["Payments:MoMo:ReturnUrl"] ?? "";
        var ipn       = _config["Payments:MoMo:IpnUrl"] ?? "";
        const string extraData = "";
        const string requestType = "captureWallet";

        var raw = $"accessKey={AccessKey}&amount={amt}&extraData={extraData}&ipnUrl={ipn}&orderId={orderId}" +
                  $"&orderInfo={orderInfo}&partnerCode={PartnerCode}&redirectUrl={redirect}" +
                  $"&requestId={requestId}&requestType={requestType}";
        var signature = HmacSha256(SecretKey, raw);

        var payload = new
        {
            partnerCode = PartnerCode,
            accessKey   = AccessKey,
            requestId,
            amount      = amt,
            orderId,
            orderInfo,
            redirectUrl = redirect,
            ipnUrl      = ipn,
            extraData,
            requestType,
            signature,
            lang        = "vi",
        };

        var http = _httpFactory.CreateClient();
        using var resp = await http.PostAsync(Endpoint,
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        var body = await resp.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var resultCode = root.TryGetProperty("resultCode", out var rc) ? rc.GetInt32() : -1;
        if (resultCode != 0)
        {
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "unknown error";
            throw new InvalidOperationException($"MoMo create failed (resultCode {resultCode}): {msg}");
        }
        var payUrl = root.TryGetProperty("payUrl", out var pu) ? pu.GetString() : null;
        return new PaymentInitiation(orderId, payUrl);
    }

    // Real providers are callback-authoritative: never approve a synchronous owner confirm.
    public Task<PaymentVerification> VerifyPaymentAsync(string paymentReference, double expectedAmount)
        => Task.FromResult(new PaymentVerification(false, "MoMo payments are confirmed via the signed IPN callback."));

    public PaymentCallbackResult ParseCallback(IReadOnlyDictionary<string, string> data)
    {
        if (string.IsNullOrWhiteSpace(SecretKey))
        {
            return new PaymentCallbackResult(false, Guid.Empty, "", "MoMo is not configured.");
        }

        string G(string k) => data.TryGetValue(k, out var v) ? v : "";
        var raw = $"accessKey={AccessKey}&amount={G("amount")}&extraData={G("extraData")}&message={G("message")}" +
                  $"&orderId={G("orderId")}&orderInfo={G("orderInfo")}&orderType={G("orderType")}" +
                  $"&partnerCode={G("partnerCode")}&payType={G("payType")}&requestId={G("requestId")}" +
                  $"&responseTime={G("responseTime")}&resultCode={G("resultCode")}&transId={G("transId")}";
        var expected = HmacSha256(SecretKey, raw);
        if (!string.Equals(expected, G("signature"), StringComparison.OrdinalIgnoreCase))
        {
            return new PaymentCallbackResult(false, Guid.Empty, "", "MoMo signature mismatch.");
        }

        var reference = G("transId");
        if (!Guid.TryParseExact(G("orderId"), "N", out var invoiceId))
        {
            return new PaymentCallbackResult(false, Guid.Empty, reference, "Missing/invalid MoMo orderId.");
        }

        if (G("resultCode") == "0")
        {
            return new PaymentCallbackResult(true, invoiceId, reference);
        }
        return new PaymentCallbackResult(false, invoiceId, reference, $"MoMo resultCode {G("resultCode")}.");
    }

    private static string HmacSha256(string key, string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }
}
