using Cinema.Business.Contracts.Payments;

namespace Cinema.Business.Payments;

/// <summary>
/// Development/sandbox payment gateway. Approves any non-empty reference for a positive amount so
/// dev and test flows complete end-to-end, while exposing the exact seam a real provider plugs into.
///
/// A production provider (VNPay/MoMo/Stripe) implements the same <see cref="IPaymentGateway"/> and is
/// selected via <c>Payments:Provider</c>. Unlike the real providers, the sandbox also approves the
/// synchronous <see cref="VerifyPaymentAsync"/> path so local dev doesn't need a live callback.
/// </summary>
public class SandboxPaymentGateway : IPaymentGateway
{
    public string Name => "Sandbox";

    /// <summary>The sandbox needs no credentials, so it is always available.</summary>
    public bool IsConfigured => true;

    public Task<PaymentInitiation> CreatePaymentAsync(Guid invoiceId, double amount, string? returnUrl)
        => Task.FromResult(new PaymentInitiation($"SANDBOX-{invoiceId:N}", returnUrl));

    public Task<PaymentVerification> VerifyPaymentAsync(string paymentReference, double expectedAmount)
    {
        var ok = !string.IsNullOrWhiteSpace(paymentReference) && expectedAmount > 0;
        return Task.FromResult(ok
            ? new PaymentVerification(true)
            : new PaymentVerification(false, "Invalid payment reference or amount."));
    }

    public Task<PaymentVerification> RefundAsync(string paymentReference, double amount)
    {
        var ok = !string.IsNullOrWhiteSpace(paymentReference) && amount > 0;
        return Task.FromResult(ok
            ? new PaymentVerification(true)
            : new PaymentVerification(false, "Invalid payment reference or amount."));
    }

    public PaymentCallbackResult ParseCallback(IReadOnlyDictionary<string, string> data)
    {
        var reference = data.TryGetValue("reference", out var r) && !string.IsNullOrWhiteSpace(r)
            ? r
            : "SANDBOX-CALLBACK";
        if (data.TryGetValue("invoiceId", out var idStr) && Guid.TryParse(idStr, out var invoiceId))
        {
            return new PaymentCallbackResult(true, invoiceId, reference);
        }
        return new PaymentCallbackResult(false, Guid.Empty, reference, "Missing or invalid invoiceId.");
    }
}
