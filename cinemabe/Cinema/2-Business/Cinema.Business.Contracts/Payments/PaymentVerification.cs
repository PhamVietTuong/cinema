namespace Cinema.Business.Contracts.Payments;

/// <summary>Result of verifying a payment against the gateway.</summary>
public record PaymentVerification(bool Success, string? FailureReason = null);
