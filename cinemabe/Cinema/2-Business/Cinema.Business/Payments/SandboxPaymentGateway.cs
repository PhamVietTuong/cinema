using Cinema.Business.Contracts.Payments;

namespace Cinema.Business.Payments;

/// <summary>
/// Development/sandbox payment gateway. Approves any non-empty reference for a positive amount so
/// dev and test flows complete end-to-end, while exposing the exact seam a real provider plugs into.
///
/// A production provider (VNPay/MoMo/Stripe) implements the same <see cref="IPaymentGateway"/>:
/// CreatePaymentAsync calls the provider to open a checkout session; VerifyPaymentAsync validates a
/// signed callback/webhook (or does a server-to-server lookup) AND confirms the captured amount
/// equals the expected amount. Register it in place of this class in DependencyInjection.
/// </summary>
public class SandboxPaymentGateway : IPaymentGateway
{
    public string Name => "Sandbox";

    public Task<PaymentInitiation> CreatePaymentAsync(Guid invoiceId, double amount, string? returnUrl)
        => Task.FromResult(new PaymentInitiation($"SANDBOX-{invoiceId:N}", returnUrl));

    public Task<PaymentVerification> VerifyPaymentAsync(string paymentReference, double expectedAmount)
    {
        var ok = !string.IsNullOrWhiteSpace(paymentReference) && expectedAmount > 0;
        return Task.FromResult(ok
            ? new PaymentVerification(true)
            : new PaymentVerification(false, "Invalid payment reference or amount."));
    }
}
