namespace Cinema.Service.WebApiHost.Controllers;

public record InitiatePaymentRequest(Guid InvoiceId, string? Provider, string? ReturnUrl);
