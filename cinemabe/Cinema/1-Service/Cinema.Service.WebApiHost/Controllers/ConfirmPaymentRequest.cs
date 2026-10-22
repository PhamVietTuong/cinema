namespace Cinema.Service.WebApiHost.Controllers;

public record ConfirmPaymentRequest(Guid InvoiceId, string PaymentReference);
