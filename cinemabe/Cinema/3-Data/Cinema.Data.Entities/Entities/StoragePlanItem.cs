namespace Cinema.Data.Entities;

public class StoragePlanItem : BaseEntity
{
    public Guid StoragePlanId { get; set; }
    public StoragePlan StoragePlan { get; set; } = null!;
    public Guid FoodAndDrinkId { get; set; }
    public FoodAndDrink FoodAndDrink { get; set; } = null!;
    public int PlannedQuantity { get; set; }
    /// <summary>Filled in when the plan is received (defaults to the planned quantity).</summary>
    public int? ReceivedQuantity { get; set; }
    /// <summary>Optional purchase cost per unit; no logic depends on it yet.</summary>
    public double? UnitCost { get; set; }
    public string? Note { get; set; }
}
