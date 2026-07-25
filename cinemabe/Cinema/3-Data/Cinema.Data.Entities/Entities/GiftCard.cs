namespace Cinema.Data.Entities;

/// <summary>
/// A stored-value gift card / voucher. Issued by an admin with an initial balance; the remaining
/// <see cref="Balance"/> is drawn down as it's applied to bookings and restored if a booking is
/// cancelled, expires, or is refunded.
/// </summary>
public class GiftCard : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public double InitialBalance { get; set; }
    public double Balance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    /// <summary>Optional recipient email (for records / delivery); not required to redeem.</summary>
    public string? IssuedToEmail { get; set; }
}
