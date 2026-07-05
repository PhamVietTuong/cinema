namespace Cinema.Business.Contracts.Payments;

/// <summary>Result of asking the gateway to start a payment for an invoice.</summary>
public record PaymentInitiation(string PaymentReference, string? RedirectUrl);

/// <summary>Result of verifying a payment against the gateway.</summary>
public record PaymentVerification(bool Success, string? FailureReason = null);

/// <summary>
/// Abstraction over a payment provider. The default implementation is a dev sandbox;
/// swap in VNPay / MoMo / Stripe behind this interface without touching booking logic.
/// </summary>
public interface IPaymentGateway
{
    string Name { get; }

    /// <summary>Begins a payment; returns a provider reference (and an optional redirect URL for hosted checkout).</summary>
    Task<PaymentInitiation> CreatePaymentAsync(Guid invoiceId, double amount, string? returnUrl);

    /// <summary>Confirms the payment really happened and the captured amount matches <paramref name="expectedAmount"/>.</summary>
    Task<PaymentVerification> VerifyPaymentAsync(string paymentReference, double expectedAmount);
}
