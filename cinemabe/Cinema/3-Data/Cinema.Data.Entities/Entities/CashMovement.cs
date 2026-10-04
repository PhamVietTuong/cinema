using Cinema.Data.Enums;

namespace Cinema.Data.Entities;

/// <summary>
/// Signed cash change in a drawer session: positive money in (opening float, cash sale, pay-in), negative money
/// out (refund, pay-out). The expected cash of a session is the sum of its movements.
/// </summary>
public class CashMovement : BaseEntity
{
    public Guid CashDrawerSessionId { get; set; }
    public Guid TheaterId { get; set; }
    public CashMovementType Type { get; set; }
    public double Amount { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid UserId { get; set; }
    public string? Note { get; set; }
    public CashDrawerSession CashDrawerSession { get; set; } = null!;
}
