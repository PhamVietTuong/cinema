using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Inventory;

public class InventoryItemDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public double Price { get; set; }
    public bool IsAvailable { get; set; }
    public bool TrackInventory { get; set; }
    public int QuantityOnHand { get; set; }
    public int LowStockThreshold { get; set; }
    public int TargetStockLevel { get; set; }

    public bool IsLowStock
    {
        get { return TrackInventory && QuantityOnHand <= LowStockThreshold; }
    }

    public bool IsOutOfStock
    {
        get { return TrackInventory && QuantityOnHand == 0; }
    }

    /// <summary>How many units bring the item back up to its target level (0 when untracked or no target is set).</summary>
    public int SuggestedReorderQuantity
    {
        get
        {
            if (TrackInventory && TargetStockLevel > 0)
            {
                return Math.Max(0, TargetStockLevel - QuantityOnHand);
            }
            return 0;
        }
    }
}

public class UpdateInventorySettingsRequest
{
    [NotEmptyGuid]
    public Guid FoodAndDrinkId { get; set; }

    public bool TrackInventory { get; set; }

    [Range(0, 100000)]
    public int LowStockThreshold { get; set; }

    [Range(0, 100000)]
    public int TargetStockLevel { get; set; }

    /// <summary>Required when tracking is switched on for an item that was not tracked before.</summary>
    [Range(0, 100000)]
    public int? OpeningQuantity { get; set; }
}

public class RecordStockMovementRequest
{
    [NotEmptyGuid]
    public Guid FoodAndDrinkId { get; set; }

    /// <summary>Only Adjust (signed, non-zero delta) or Waste (positive quantity, stored negative).</summary>
    public StockMovementType Type { get; set; }

    public int Quantity { get; set; }

    public StockReasonCode ReasonCode { get; set; }

    /// <summary>Required when <see cref="ReasonCode"/> is Other.</summary>
    [StringLength(500)]
    public string? Note { get; set; }
}

public class RecordStockCountRequest
{
    [NotEmptyGuid]
    public Guid FoodAndDrinkId { get; set; }

    [Range(0, 100000)]
    public int CountedQuantity { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public class StockMovementDTO
{
    public Guid Id { get; set; }
    public Guid FoodAndDrinkId { get; set; }
    public string? FoodAndDrinkName { get; set; }
    public Guid TheaterId { get; set; }
    public StockMovementType Type { get; set; }
    public int Quantity { get; set; }
    public StockReasonCode? ReasonCode { get; set; }
    public string? Reason { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceCode { get; set; }
    public Guid? StoragePlanId { get; set; }
    public string? StoragePlanCode { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime CreationTime { get; set; }
}
