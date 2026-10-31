namespace Cinema.Business.Contracts.Payments;

/// <summary>
/// Result of parsing &amp; signature-verifying a provider callback (IPN / redirect / webhook).
/// This is the authoritative "money really moved" signal for real providers.
/// </summary>
public record PaymentCallbackResult(bool Success, Guid InvoiceId, string PaymentReference, string? FailureReason = null)
{
    /// <summary>Defaults from <see cref="Success"/> for gateways that don't set it explicitly.</summary>
    public PaymentCallbackStatus Status { get; init; } = DefaultStatus(Success);

    /// <summary>The amount the provider says it charged, in major currency units (e.g. VND).
    /// Null means the gateway doesn't report one, so the amount check is skipped.</summary>
    public decimal? ReceivedAmount { get; init; }

    private static PaymentCallbackStatus DefaultStatus(bool success)
    {
        if (success)
        {
            return PaymentCallbackStatus.Paid;
        }
        return PaymentCallbackStatus.Rejected;
    }
}
