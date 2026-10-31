namespace Cinema.Business.Contracts.Payments;

/// <summary>What <c>IBookingManager.HandlePaymentCallbackAsync</c> decided to do with a provider callback.</summary>
public enum PaymentCallbackOutcome
{
    /// <summary>The invoice was just finalized as Paid.</summary>
    Confirmed,
    /// <summary>The provider reported a failed/cancelled payment; acknowledged, invoice left untouched.</summary>
    PaymentFailedAcknowledged,
    /// <summary>The invoice was already Paid (duplicate callback delivery).</summary>
    AlreadyConfirmed,
    /// <summary>The invoice exists but is neither Pending nor Paid (e.g. Cancelled).</summary>
    NotPending,
    /// <summary>No invoice matches the callback's order reference.</summary>
    OrderNotFound,
    /// <summary>The callback's amount did not match the invoice's expected amount.</summary>
    InvalidAmount,
    /// <summary>The callback's signature/checksum did not verify.</summary>
    InvalidSignature,
    /// <summary>Generic rejection: gateway not configured, unrecognized event, etc.</summary>
    Rejected,
    /// <summary>An unhandled exception occurred while processing the callback.</summary>
    Error,
}
