namespace Cinema.Business.Contracts.Payments;

/// <summary>
/// Result of parsing &amp; signature-verifying a provider callback (IPN / redirect / webhook).
/// This is the authoritative "money really moved" signal for real providers.
/// </summary>
public record PaymentCallbackResult(bool Success, Guid InvoiceId, string PaymentReference, string? FailureReason = null);
