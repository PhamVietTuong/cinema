namespace Cinema.Business.Helpers;

/// <summary>
/// Money rules shared by more than one place. The whole-dong amount a payment gateway is asked to collect is derived
/// here once, so the amount sent to the gateway and the amount a callback is checked against can never drift apart.
/// </summary>
public static class PaymentAmounts
{
    /// <summary>
    /// Whole dong the gateway collects for an invoice total. Uses the framework's default rounding (to even) on
    /// purpose: it is what both the payment request and the callback amount check have always used, so changing the
    /// mode here changes what customers are charged. Invoice totals are whole dong in practice, so the mode only
    /// matters for a stray .5.
    /// </summary>
    public static long GatewayAmount(double invoiceTotal)
    {
        return (long)Math.Round(invoiceTotal);
    }
}
