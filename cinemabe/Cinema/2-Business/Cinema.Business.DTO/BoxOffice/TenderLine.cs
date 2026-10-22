using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Staff;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.BoxOffice;

/// <summary>One payment the customer hands over. Only Cash, Card and QrWallet are accepted here: points and gift cards
/// are applied through <see cref="CounterSaleRequest.PointsToRedeem"/> / <see cref="CounterSaleRequest.GiftCardCode"/>.</summary>
public class TenderLine
{
    public PaymentTender Method { get; set; }

    /// <summary>Cash: the amount handed over (change is computed). Card/QR: the amount charged on the terminal.</summary>
    [Range(0, 100000000)]
    public double Amount { get; set; }

    /// <summary>Terminal / wallet reference. Required for Card and QrWallet.</summary>
    [StringLength(100)]
    public string? Reference { get; set; }
}
