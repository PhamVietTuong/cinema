using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Concession;

/// <summary>A tracked item at or under its low-stock threshold. Also the <c>StockLow</c> hub event payload.</summary>
public class LowStockItemDTO
{
    public Guid FoodAndDrinkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int LowStockThreshold { get; set; }
    public int TargetStockLevel { get; set; }
}
