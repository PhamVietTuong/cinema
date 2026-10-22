namespace Cinema.Business.Contracts.Payments;

/// <summary>Result of asking the gateway to start a payment for an invoice.</summary>
public record PaymentInitiation(string PaymentReference, string? RedirectUrl);
