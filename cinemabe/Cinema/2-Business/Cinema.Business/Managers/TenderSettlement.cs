using Cinema.Business.DTO.BoxOffice;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public sealed class TenderSettlementResult
{
    /// <summary>One cash row (if any cash was handed over) plus one row per Card/QR tender.</summary>
    public List<InvoicePayment> Payments { get; init; } = new();

    /// <summary>The part of the price actually paid in cash (what enters the drawer).</summary>
    public double CashApplied { get; init; }

    /// <summary>Cash handed back to the customer.</summary>
    public double ChangeDue { get; init; }
}

/// <summary>
/// Pure rules for splitting a counter sale's amount due across tenders. Cash is the only tender that may overpay
/// (change is returned); Card and QrWallet are exact and need a terminal reference. Points and gift cards are not
/// tenders here: they are applied while pricing the sale (<c>PointsToRedeem</c> / <c>GiftCardCode</c>).
/// All amounts are whole VND.
/// </summary>
public static class TenderSettlement
{
    private const int _maxReferenceLength = 100;

    public static TenderSettlementResult Settle(double finalAmount, IReadOnlyList<TenderLine> tenders)
    {
        var due = Whole(finalAmount);
        double cashTendered = 0;
        double nonCash = 0;
        var payments = new List<InvoicePayment>();

        foreach (var tender in tenders)
        {
            var amount = Whole(tender.Amount);
            if (amount <= 0)
            {
                throw new InvalidOperationException("Every tender must be greater than zero.");
            }

            switch (tender.Method)
            {
                case PaymentTender.Cash:
                    cashTendered += amount;
                    break;
                case PaymentTender.Card:
                case PaymentTender.QrWallet:
                    var reference = tender.Reference?.Trim();
                    if (string.IsNullOrEmpty(reference))
                    {
                        throw new InvalidOperationException($"A reference is required for a {tender.Method} payment.");
                    }
                    if (reference.Length > _maxReferenceLength)
                    {
                        throw new InvalidOperationException("The payment reference is too long.");
                    }
                    nonCash += amount;
                    payments.Add(new InvoicePayment { Method = tender.Method, Amount = amount, Reference = reference });
                    break;
                default:
                    throw new InvalidOperationException("Only Cash, Card and QrWallet can be tendered; use points or a gift card code to apply those.");
            }
        }

        if (nonCash > due)
        {
            throw new InvalidOperationException("Card and QR payments exceed the amount due.");
        }

        var cashDue = due - nonCash;
        if (cashTendered < cashDue)
        {
            throw new InvalidOperationException($"The payment is short by {cashDue - cashTendered:0} VND.");
        }
        if (cashTendered > 0 && cashDue <= 0)
        {
            throw new InvalidOperationException("No cash is due for this sale.");
        }

        var change = cashTendered - cashDue;
        if (cashTendered > 0)
        {
            payments.Insert(0, new InvoicePayment
            {
                Method         = PaymentTender.Cash,
                Amount         = cashDue,
                TenderedAmount = cashTendered,
                ChangeAmount   = change,
            });
        }

        return new TenderSettlementResult { Payments = payments, CashApplied = cashDue, ChangeDue = change };
    }

    private static double Whole(double value)
    {
        return Math.Round(value, 0, MidpointRounding.AwayFromZero);
    }
}
