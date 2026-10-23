namespace Cinema.Business.Contracts.Payments;

/// <summary>
/// Abstraction over a payment provider. The dev default is <c>Sandbox</c>; VNPay / MoMo / Stripe
/// plug in behind this interface without touching booking logic. Multiple implementations are
/// registered and picked per-invoice through <see cref="IPaymentGatewayResolver"/>.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Stable provider key (e.g. "Sandbox", "VNPay", "MoMo", "Stripe"). Stored on the invoice.</summary>
    string Name { get; }

    /// <summary>
    /// True when this provider has the credentials it needs to actually take a payment.
    /// The resolver refuses to hand out an unconfigured gateway, so a half-set-up provider
    /// surfaces as a clear error at selection time instead of throwing mid-checkout.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Begins a payment; returns a provider reference and (usually) a redirect URL for hosted checkout.
    /// <paramref name="clientIp"/> is the end-user's IP, which some providers require (VNPay's
    /// <c>vnp_IpAddr</c>); gateways that don't need it ignore the parameter.
    /// </summary>
    Task<PaymentInitiation> CreatePaymentAsync(Guid invoiceId, double amount, string? returnUrl, string? clientIp);

    /// <summary>
    /// Synchronous, owner-initiated confirm. Only the dev <c>Sandbox</c> gateway approves here;
    /// real providers return <c>Success = false</c> because the signed callback (<see cref="ParseCallback"/>)
    /// is the only authority — this prevents a user self-confirming a payment that never happened.
    /// </summary>
    Task<PaymentVerification> VerifyPaymentAsync(string paymentReference, double expectedAmount);

    /// <summary>
    /// Parses &amp; signature-verifies a provider server-to-server callback. Input is the flattened
    /// request (query + form params; the raw body is under <c>__rawBody</c> and any signature header
    /// under <c>__signature</c>). Returns whether the payment succeeded plus the invoice + reference.
    /// </summary>
    PaymentCallbackResult ParseCallback(IReadOnlyDictionary<string, string> data);
}
