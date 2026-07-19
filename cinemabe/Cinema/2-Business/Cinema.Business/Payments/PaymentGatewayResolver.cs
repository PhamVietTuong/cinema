using Cinema.Business.Contracts.Payments;

namespace Cinema.Business.Payments;

/// <summary>
/// Holds every registered gateway keyed by <see cref="IPaymentGateway.Name"/> and resolves one by name.
/// The default provider comes from <c>Payments:Provider</c> config; when it names an unregistered/unknown
/// provider the first registered gateway (Sandbox) is used, so dev never breaks.
/// </summary>
public class PaymentGatewayResolver : IPaymentGatewayResolver
{
    private readonly Dictionary<string, IPaymentGateway> _byName;

    public IPaymentGateway Default { get; }

    public PaymentGatewayResolver(IEnumerable<IPaymentGateway> gateways, string? defaultProvider = null)
    {
        _byName = gateways.ToDictionary(g => g.Name, StringComparer.OrdinalIgnoreCase);
        if (_byName.Count == 0)
        {
            throw new InvalidOperationException("No payment gateways registered.");
        }

        if (!string.IsNullOrWhiteSpace(defaultProvider) && _byName.TryGetValue(defaultProvider, out var configured))
        {
            Default = configured;
        }
        else
        {
            Default = _byName.Values.First();
        }
    }

    public IPaymentGateway Resolve(string? providerName)
    {
        if (!string.IsNullOrWhiteSpace(providerName) && _byName.TryGetValue(providerName, out var gateway))
        {
            return gateway;
        }
        return Default;
    }
}
