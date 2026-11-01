namespace Cinema.Business.DTO.Requests;

public class ConfirmPaymentRequest
{
    public Guid InvoiceId { get; set; }

    public string PaymentReference { get; set; } = string.Empty;
}
