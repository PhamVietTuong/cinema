using Cinema.Data.Enums;

namespace Cinema.Data.Entities;

/// <summary>A cashier's drawer shift at one terminal. At most one Open session per user and per terminal.</summary>
public class CashDrawerSession : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Guid UserId { get; set; }
    public string TerminalName { get; set; } = string.Empty;
    public CashDrawerStatus Status { get; set; } = CashDrawerStatus.Open;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public double OpeningFloat { get; set; }
    /// <summary>Filled when the drawer is closed/reconciled (phase P5).</summary>
    public double? CountedCash { get; set; }
    public double? ExpectedCash { get; set; }
    public double? Variance { get; set; }
    public ICollection<CashMovement> Movements { get; set; } = new List<CashMovement>();
}
