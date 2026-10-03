using Cinema.Data.Enums;

namespace Cinema.Data.Entities;

/// <summary>
/// Insert-only stock ledger row. <see cref="Quantity"/> is signed (+ adds stock, − removes it). The cached
/// <c>FoodAndDrink.QuantityOnHand</c> is only ever changed together with one of these rows.
/// </summary>
public class StockMovement : BaseEntity
{
    public Guid FoodAndDrinkId { get; set; }
    public FoodAndDrink FoodAndDrink { get; set; } = null!;
    /// <summary>Denormalised for theater scoping and reporting.</summary>
    public Guid TheaterId { get; set; }
    public StockMovementType Type { get; set; }
    public int Quantity { get; set; }
    /// <summary>Set for Adjust/Waste. Null for system movements.</summary>
    public StockReasonCode? ReasonCode { get; set; }
    public string? Reason { get; set; }
    /// <summary>No FK, so the ledger survives invoice deletion.</summary>
    public Guid? InvoiceId { get; set; }
    public Guid? StoragePlanId { get; set; }
    /// <summary>Null means the system (e.g. the expiry reaper).</summary>
    public Guid? UserId { get; set; }
}
