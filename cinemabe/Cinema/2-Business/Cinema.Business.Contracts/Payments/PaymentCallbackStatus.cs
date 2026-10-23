namespace Cinema.Business.Contracts.Payments;

/// <summary>What a gateway's <c>ParseCallback</c> learned about a single provider callback.</summary>
public enum PaymentCallbackStatus
{
    /// <summary>Signature valid, provider reports the payment succeeded.</summary>
    Paid,
    /// <summary>Signature valid, provider reports the payment failed/was cancelled.</summary>
    PaymentFailed,
    /// <summary>The callback's signature/checksum did not verify.</summary>
    InvalidSignature,
    /// <summary>The order reference (e.g. vnp_TxnRef) was missing or could not be parsed.</summary>
    InvalidOrderReference,
    /// <summary>The amount field was missing, malformed, or did not match the expected invoice amount.</summary>
    InvalidAmount,
    /// <summary>Generic rejection: gateway not configured, unrecognized event, etc.</summary>
    Rejected,
}
