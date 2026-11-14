namespace Cinema.Data.Enums;

/// <summary>How part of an invoice was paid. Stored as int in <c>InvoicePayment.Method</c>; never renumber.</summary>
public enum PaymentTender
{
    Cash = 0,
    Card = 1,
    QrWallet = 2,
    // 3 was retired when gift cards were removed (2026-11-14): never reuse the value (the upgrade script deleted every row that carried it).
    Points = 4,
    Online = 5
}
