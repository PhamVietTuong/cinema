namespace Cinema.Data.Contracts;

/// <summary>Read-only projection of a tracked item at or under its low-stock threshold.</summary>
public sealed class LowStockRow
{
    public Guid FoodAndDrinkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int LowStockThreshold { get; set; }
    public int TargetStockLevel { get; set; }
}
