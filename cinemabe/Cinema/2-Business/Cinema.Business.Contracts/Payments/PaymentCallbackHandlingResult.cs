namespace Cinema.Business.Contracts.Payments;

/// <summary>Outcome of handling a provider callback, plus which gateway actually resolved it
/// (used to pick the right acknowledgement shape per provider).</summary>
public record PaymentCallbackHandlingResult(string Provider, PaymentCallbackOutcome Outcome);
