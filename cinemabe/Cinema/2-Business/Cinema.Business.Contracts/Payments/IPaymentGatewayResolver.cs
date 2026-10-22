namespace Cinema.Business.Contracts.Payments;

/// <summary>Selects a registered <see cref="IPaymentGateway"/> by name, falling back to the configured default.</summary>
public interface IPaymentGatewayResolver
{
    /// <summary>The provider chosen by <c>Payments:Provider</c> config (Sandbox when unset).</summary>
    IPaymentGateway Default { get; }

    /// <summary>Returns the named gateway, or <see cref="Default"/> when the name is null/unknown.</summary>
    IPaymentGateway Resolve(string? providerName);
}
