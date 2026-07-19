namespace Cinema.Business.Contracts.Payments;

/// <summary>Result of asking the gateway to start a payment for an invoice.</summary>
public record PaymentInitiation(string PaymentReference, string? RedirectUrl);

/// <summary>Result of verifying a payment against the gateway.</summary>
public record PaymentVerification(bool Success, string? FailureReason = null);

/// <summary>
/// Result of parsing &amp; signature-verifying a provider callback (IPN / redirect / webhook).
/// This is the authoritative "money really moved" signal for real providers.
/// </summary>
public record PaymentCallbackResult(bool Success, Guid InvoiceId, string PaymentReference, string? FailureReason = null);

/// <summary>
/// Abstraction over a payment provider. The dev default is <c>Sandbox</c>; VNPay / MoMo / Stripe
/// plug in behind this interface without touching booking logic. Multiple implementations are
/// registered and picked per-invoice through <see cref="IPaymentGatewayResolver"/>.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Stable provider key (e.g. "Sandbox", "VNPay", "MoMo", "Stripe"). Stored on the invoice.</summary>
    string Name { get; }

    /// <summary>Begins a payment; returns a provider reference and (usually) a redirect URL for hosted checkout.</summary>
    Task<PaymentInitiation> CreatePaymentAsync(Guid invoiceId, double amount, string? returnUrl);

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

    /// <summary>
    /// Refunds a previously captured payment (server-to-server, authenticated). Unlike payment capture,
    /// a refund needs no user redirect, so this is synchronous. Returns success or a failure reason.
    /// </summary>
    Task<PaymentVerification> RefundAsync(string paymentReference, double amount);
}

/// <summary>Selects a registered <see cref="IPaymentGateway"/> by name, falling back to the configured default.</summary>
public interface IPaymentGatewayResolver
{
    /// <summary>The provider chosen by <c>Payments:Provider</c> config (Sandbox when unset).</summary>
    IPaymentGateway Default { get; }

    /// <summary>Returns the named gateway, or <see cref="Default"/> when the name is null/unknown.</summary>
    IPaymentGateway Resolve(string? providerName);
}
