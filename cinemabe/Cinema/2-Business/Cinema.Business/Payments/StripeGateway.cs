using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cinema.Business.Contracts.Payments;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Payments;

/// <summary>
/// Stripe gateway (international cards). We create a hosted Checkout Session via the Stripe API and
/// redirect the user to its URL. Stripe then POSTs a <c>checkout.session.completed</c> webhook whose raw
/// body is signed; <see cref="ParseCallback"/> verifies the <c>Stripe-Signature</c> HMAC-SHA256 before
/// trusting it. Amounts use VND (a zero-decimal currency), so unit_amount is the plain VND integer.
///
/// Config under <c>Payments:Stripe</c>: <c>SecretKey</c>, <c>WebhookSecret</c>, <c>SuccessUrl</c>,
/// <c>CancelUrl</c>. This talks to Stripe over HTTP directly (no SDK dependency).
/// </summary>
public class StripeGateway : IPaymentGateway
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;

    public StripeGateway(IConfiguration config, IHttpClientFactory httpFactory)
    {
        _config = config;
        _httpFactory = httpFactory;
    }

    public string Name => "Stripe";

    private string SecretKey     => _config["Payments:Stripe:SecretKey"]     ?? "";
    private string WebhookSecret => _config["Payments:Stripe:WebhookSecret"] ?? "";

    public async Task<PaymentInitiation> CreatePaymentAsync(Guid invoiceId, double amount, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(SecretKey))
        {
            throw new InvalidOperationException("Stripe is not configured (Payments:Stripe:SecretKey).");
        }

        var invoiceRef = invoiceId.ToString("N");
        var unitAmount = ((long)Math.Round(amount)).ToString(CultureInfo.InvariantCulture);
        var success = returnUrl ?? _config["Payments:Stripe:SuccessUrl"] ?? "";
        var cancel  = _config["Payments:Stripe:CancelUrl"] ?? success;

        var form = new Dictionary<string, string>
        {
            ["mode"]                = "payment",
            ["success_url"]         = success,
            ["cancel_url"]          = cancel,
            ["client_reference_id"] = invoiceRef,
            ["metadata[invoiceId]"] = invoiceRef,
            ["line_items[0][price_data][currency]"]               = "vnd",
            ["line_items[0][price_data][product_data][name]"]     = $"Cinema booking {invoiceRef}",
            ["line_items[0][price_data][unit_amount]"]            = unitAmount,
            ["line_items[0][quantity]"]                           = "1",
        };

        var http = _httpFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/checkout/sessions")
        {
            Content = new FormUrlEncodedContent(form),
        };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SecretKey);

        using var resp = await http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (!resp.IsSuccessStatusCode)
        {
            var msg = root.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var m)
                ? m.GetString()
                : "unknown error";
            throw new InvalidOperationException($"Stripe create session failed: {msg}");
        }

        var sessionId = root.TryGetProperty("id", out var id) ? id.GetString() ?? invoiceRef : invoiceRef;
        var url       = root.TryGetProperty("url", out var u) ? u.GetString() : null;
        return new PaymentInitiation(sessionId, url);
    }

    // Real providers are callback-authoritative: never approve a synchronous owner confirm.
    public Task<PaymentVerification> VerifyPaymentAsync(string paymentReference, double expectedAmount)
        => Task.FromResult(new PaymentVerification(false, "Stripe payments are confirmed via the signed webhook."));

    public async Task<PaymentVerification> RefundAsync(string paymentReference, double amount)
    {
        if (string.IsNullOrWhiteSpace(SecretKey))
        {
            return new PaymentVerification(false, "Stripe is not configured.");
        }

        var form = new Dictionary<string, string>
        {
            ["payment_intent"] = paymentReference,
            ["amount"]         = ((long)Math.Round(amount)).ToString(CultureInfo.InvariantCulture),
        };
        var http = _httpFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/refunds")
        {
            Content = new FormUrlEncodedContent(form),
        };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SecretKey);

        using var resp = await http.SendAsync(req);
        if (resp.IsSuccessStatusCode)
        {
            return new PaymentVerification(true);
        }

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var msg = doc.RootElement.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var m)
            ? m.GetString()
            : "unknown error";
        return new PaymentVerification(false, $"Stripe refund failed: {msg}");
    }

    public PaymentCallbackResult ParseCallback(IReadOnlyDictionary<string, string> data)
    {
        if (string.IsNullOrWhiteSpace(WebhookSecret))
        {
            return new PaymentCallbackResult(false, Guid.Empty, "", "Stripe is not configured.");
        }
        if (!data.TryGetValue("__rawBody", out var payload) || string.IsNullOrEmpty(payload))
        {
            return new PaymentCallbackResult(false, Guid.Empty, "", "Missing Stripe webhook body.");
        }
        if (!data.TryGetValue("__signature", out var sigHeader) || string.IsNullOrEmpty(sigHeader))
        {
            return new PaymentCallbackResult(false, Guid.Empty, "", "Missing Stripe-Signature header.");
        }

        // Stripe-Signature: "t=<timestamp>,v1=<sig>[,v1=<sig>...]"
        string? t = null;
        var v1 = new List<string>();
        foreach (var part in sigHeader.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2)
            {
                continue;
            }
            if (kv[0] == "t")
            {
                t = kv[1];
            }
            else if (kv[0] == "v1")
            {
                v1.Add(kv[1]);
            }
        }
        if (t is null || v1.Count == 0)
        {
            return new PaymentCallbackResult(false, Guid.Empty, "", "Malformed Stripe-Signature.");
        }

        var expected = HmacSha256Hex(WebhookSecret, $"{t}.{payload}");
        var signatureValid = v1.Any(s => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(s), Encoding.UTF8.GetBytes(expected)));
        if (!signatureValid)
        {
            return new PaymentCallbackResult(false, Guid.Empty, "", "Stripe signature mismatch.");
        }

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var type = root.TryGetProperty("type", out var ty) ? ty.GetString() : "";
        if (type != "checkout.session.completed")
        {
            return new PaymentCallbackResult(false, Guid.Empty, "", $"Ignored Stripe event '{type}'.");
        }

        var obj = root.GetProperty("data").GetProperty("object");
        var invoiceRef = obj.TryGetProperty("client_reference_id", out var cr) ? cr.GetString() : null;
        var reference  = obj.TryGetProperty("payment_intent", out var pi) ? pi.GetString() ?? "" : "";
        var paid       = obj.TryGetProperty("payment_status", out var ps) && ps.GetString() == "paid";

        if (invoiceRef is null || !Guid.TryParseExact(invoiceRef, "N", out var invoiceId))
        {
            return new PaymentCallbackResult(false, Guid.Empty, reference, "Missing/invalid client_reference_id.");
        }

        if (paid)
        {
            return new PaymentCallbackResult(true, invoiceId, reference);
        }
        return new PaymentCallbackResult(false, invoiceId, reference, "Stripe session not paid.");
    }

    private static string HmacSha256Hex(string key, string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }
}
