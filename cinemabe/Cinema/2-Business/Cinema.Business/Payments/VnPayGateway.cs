using System.Net;
using System.Security.Cryptography;
using System.Text;
using Cinema.Business.Contracts.Payments;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Payments;

/// <summary>
/// VNPay gateway (Vietnam). Payment is a signed redirect: we build a query signed with HMAC-SHA512 over
/// the sorted parameters and send the user to <c>vpcpay.html</c>. VNPay then calls our IPN endpoint with
/// signed params — <see cref="ParseCallback"/> re-verifies that signature and the response code; that
/// callback is the only path that marks an invoice paid.
///
/// Config under <c>Payments:VnPay</c>: <c>TmnCode</c>, <c>HashSecret</c>, <c>PayUrl</c>, <c>ReturnUrl</c>.
/// Get merchant credentials from the VNPay merchant portal.
/// </summary>
public class VnPayGateway : IPaymentGateway
{
    private readonly IConfiguration _config;
    public VnPayGateway(IConfiguration config)
    {
        _config = config;
    }

    public string Name => "VNPay";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(TmnCode) && !string.IsNullOrWhiteSpace(HashSecret);

    private string TmnCode    => _config["Payments:VnPay:TmnCode"]    ?? "";
    private string HashSecret => _config["Payments:VnPay:HashSecret"] ?? "";
    private string PayUrl     => _config["Payments:VnPay:PayUrl"]     ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    public Task<PaymentInitiation> CreatePaymentAsync(Guid invoiceId, double amount, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(TmnCode) || string.IsNullOrWhiteSpace(HashSecret))
        {
            throw new InvalidOperationException("VNPay is not configured (Payments:VnPay:TmnCode / HashSecret).");
        }

        var txnRef = invoiceId.ToString("N");
        var now    = DateTime.Now;
        var p = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"]   = "2.1.0",
            ["vnp_Command"]   = "pay",
            ["vnp_TmnCode"]   = TmnCode,
            ["vnp_Amount"]    = ((long)Math.Round(amount) * 100).ToString(),   // VNPay expects amount * 100
            ["vnp_CurrCode"]  = "VND",
            ["vnp_TxnRef"]    = txnRef,
            ["vnp_OrderInfo"] = $"Thanh toan don hang {txnRef}",
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"]    = "vn",
            ["vnp_ReturnUrl"] = returnUrl ?? _config["Payments:VnPay:ReturnUrl"] ?? "",
            ["vnp_IpAddr"]    = "127.0.0.1",
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
            ["vnp_ExpireDate"] = now.AddMinutes(15).ToString("yyyyMMddHHmmss"),
        };

        var signData  = string.Join("&", p.Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
        var secureHash = HmacSha512(HashSecret, signData);
        var redirectUrl = $"{PayUrl}?{signData}&vnp_SecureHash={secureHash}";

        return Task.FromResult(new PaymentInitiation(txnRef, redirectUrl));
    }

    // Real providers are callback-authoritative: never approve a synchronous owner confirm.
    public Task<PaymentVerification> VerifyPaymentAsync(string paymentReference, double expectedAmount)
        => Task.FromResult(new PaymentVerification(false, "VNPay payments are confirmed via the signed IPN callback."));

    // VNPay refunds require the merchant refund/approval workflow and are processed out-of-band via the
    // VNPay merchant portal; an admin records the completed refund in the app (see RefundBookingAsync).
    public Task<PaymentVerification> RefundAsync(string paymentReference, double amount)
        => Task.FromResult(new PaymentVerification(false, "Process VNPay refunds via the VNPay merchant portal, then record it as an admin."));

    public PaymentCallbackResult ParseCallback(IReadOnlyDictionary<string, string> data)
    {
        if (string.IsNullOrWhiteSpace(HashSecret))
        {
            return new PaymentCallbackResult(false, Guid.Empty, "", "VNPay is not configured.");
        }

        var received = data.TryGetValue("vnp_SecureHash", out var h) ? h : "";
        var signed = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in data)
        {
            if (kv.Key.StartsWith("vnp_") && kv.Key != "vnp_SecureHash" && kv.Key != "vnp_SecureHashType")
            {
                signed[kv.Key] = kv.Value;
            }
        }

        var signData = string.Join("&", signed.Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
        var expected = HmacSha512(HashSecret, signData);
        if (!string.Equals(expected, received, StringComparison.OrdinalIgnoreCase))
        {
            return new PaymentCallbackResult(false, Guid.Empty, "", "VNPay signature mismatch.");
        }

        var responseOk = data.TryGetValue("vnp_ResponseCode", out var rc) && rc == "00"
                      && (!data.TryGetValue("vnp_TransactionStatus", out var ts) || ts == "00");
        var reference = data.TryGetValue("vnp_TransactionNo", out var tn) ? tn : received;
        if (!data.TryGetValue("vnp_TxnRef", out var txnRef) || !Guid.TryParseExact(txnRef, "N", out var invoiceId))
        {
            return new PaymentCallbackResult(false, Guid.Empty, reference, "Missing/invalid vnp_TxnRef.");
        }

        if (responseOk)
        {
            return new PaymentCallbackResult(true, invoiceId, reference);
        }
        var code = data.TryGetValue("vnp_ResponseCode", out var c) ? c : "?";
        return new PaymentCallbackResult(false, invoiceId, reference, $"VNPay response code {code}.");
    }

    private static string HmacSha512(string key, string data)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }
}
