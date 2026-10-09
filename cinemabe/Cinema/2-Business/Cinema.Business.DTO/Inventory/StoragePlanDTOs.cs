using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Inventory;

public class StoragePlanListItemDTO
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid TheaterId { get; set; }
    public string? TheaterName { get; set; }
    public StoragePlanStatus Status { get; set; }
    public DateTime TargetDate { get; set; }
    public string? Supplier { get; set; }
    public int ItemCount { get; set; }
    public int TotalPlannedQuantity { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreationTime { get; set; }
}

public class StoragePlanDTO : StoragePlanListItemDTO
{
    public string? Note { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? ReceivedByName { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public List<StoragePlanItemDTO> Items { get; set; } = new();
}

public class StoragePlanItemDTO
{
    public Guid Id { get; set; }
    public Guid FoodAndDrinkId { get; set; }
    public string? FoodAndDrinkName { get; set; }
    /// <summary>Current warehouse stock of the item (for the screen).</summary>
    public int QuantityOnHand { get; set; }
    public int PlannedQuantity { get; set; }
    public int? ReceivedQuantity { get; set; }
    public double? UnitCost { get; set; }
    public string? Note { get; set; }
}

public class SaveStoragePlanRequest
{
    /// <summary>Null creates a new plan; set to edit an existing Draft or Rejected plan.</summary>
    public Guid? Id { get; set; }

    [NotEmptyGuid]
    public Guid TheaterId { get; set; }

    public DateTime TargetDate { get; set; }

    [StringLength(200)]
    public string? Supplier { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    [MinLength(1)]
    public List<SaveStoragePlanItem> Items { get; set; } = new();
}

public class SaveStoragePlanItem
{
    [NotEmptyGuid]
    public Guid FoodAndDrinkId { get; set; }

    [Range(1, 100000)]
    public int PlannedQuantity { get; set; }

    [Range(0, double.MaxValue)]
    public double? UnitCost { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public class StoragePlanDecisionRequest
{
    [NotEmptyGuid]
    public Guid Id { get; set; }

    /// <summary>Required when rejecting.</summary>
    [StringLength(500)]
    public string? Reason { get; set; }
}

public class ReceiveStoragePlanRequest
{
    [NotEmptyGuid]
    public Guid Id { get; set; }

    /// <summary>Actual quantities per plan item; items omitted here default to their planned quantity.</summary>
    public List<ReceiveStoragePlanItem>? Items { get; set; }
}

public class ReceiveStoragePlanItem
{
    [NotEmptyGuid]
    public Guid StoragePlanItemId { get; set; }

    [Range(0, 100000)]
    public int ReceivedQuantity { get; set; }
}

public class CreatePlanFromLowStockRequest
{
    [NotEmptyGuid]
    public Guid TheaterId { get; set; }
}
