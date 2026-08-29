using Cinema.Business.Contracts.Payments;

namespace Cinema.Business.Payments;

/// <summary>
/// Holds every registered gateway keyed by <see cref="IPaymentGateway.Name"/> and resolves one by name.
/// The default provider comes from <c>Payments:Provider</c> config.
///
/// Only gateways reporting <see cref="IPaymentGateway.IsConfigured"/> are selectable: a provider whose
/// credentials are blank is skipped in favour of one that works. Without this, picking a payment method
/// the deployment never configured (VNPay/MoMo/Stripe with empty settings) threw mid-checkout and left
/// the customer with a pending invoice holding their seats.
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

        if (!string.IsNullOrWhiteSpace(defaultProvider)
            && _byName.TryGetValue(defaultProvider, out var configured)
            && configured.IsConfigured)
        {
            Default = configured;
        }
        else
        {
            // Prefer any usable gateway; fall back to the first registered one so that, when nothing
            // is configured, callers still get that provider's specific "not configured" message.
            Default = _byName.Values.FirstOrDefault(g => g.IsConfigured) ?? _byName.Values.First();
        }
    }

    public IPaymentGateway Resolve(string? providerName)
    {
        if (!string.IsNullOrWhiteSpace(providerName)
            && _byName.TryGetValue(providerName, out var gateway)
            && gateway.IsConfigured)
        {
            return gateway;
        }
        return Default;
    }
}
