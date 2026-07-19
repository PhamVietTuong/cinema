namespace Cinema.Business.DTO.Invoices;

/// <summary>What the client needs to send the user to a hosted checkout for an invoice.</summary>
public class PaymentInitiationDTO
{
    /// <summary>Provider that will process the payment (e.g. "Sandbox", "VNPay", "MoMo", "Stripe").</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Provider reference stored on the invoice.</summary>
    public string PaymentReference { get; set; } = string.Empty;

    /// <summary>URL to redirect the user to for hosted checkout. Null when none is required (dev sandbox).</summary>
    public string? RedirectUrl { get; set; }
}
