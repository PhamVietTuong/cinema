namespace Cinema.Business.DTO.Requests;

public class InitiatePaymentRequest
{
    public Guid InvoiceId { get; set; }

    public string? Provider { get; set; }

    public string? ReturnUrl { get; set; }
}
