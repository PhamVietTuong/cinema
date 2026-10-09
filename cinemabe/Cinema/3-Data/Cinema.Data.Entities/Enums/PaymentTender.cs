namespace Cinema.Data.Enums;

/// <summary>How part of an invoice was paid. Stored as int in <c>InvoicePayment.Method</c>; never renumber.</summary>
public enum PaymentTender
{
    Cash = 0,
    Card = 1,
    QrWallet = 2,
    GiftCard = 3,
    Points = 4,
    Online = 5
}
