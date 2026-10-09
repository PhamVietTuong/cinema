using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Concession;

public class PickupItemDTO
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

/// <summary>One invoice with food, as the kitchen's pickup queue and the pickup lookup show it.</summary>
public class PickupOrderDTO
{
    public Guid InvoiceId { get; set; }
    public Guid TheaterId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public InvoiceStatus InvoiceStatus { get; set; }
    public SalesChannel Channel { get; set; }
    public FoodOrderStatus FoodStatus { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? FoodHandedOverAt { get; set; }
    public string? CustomerName { get; set; }
    /// <summary>Earliest showtime of the invoice; null for a food-only sale.</summary>
    public DateTime? ShowTimeStart { get; set; }
    public string? MovieTitle { get; set; }
    public string? RoomName { get; set; }
    public List<PickupItemDTO> Items { get; set; } = new();
}

public class GetPickupQueueRequest
{
    /// <summary>Required for admins (and multi-theater roles); staff may omit it.</summary>
    public Guid? TheaterId { get; set; }

    /// <summary>The (local) day whose showtimes the queue covers. Null = today.</summary>
    public DateTime? Day { get; set; }
}

public class SetFoodStatusRequest
{
    public Guid? TheaterId { get; set; }

    [NotEmptyGuid]
    public Guid InvoiceId { get; set; }

    /// <summary>The state to move to: Preparing, Ready or HandedOver (see <see cref="FoodOrderStatus"/>).</summary>
    public FoodOrderStatus Status { get; set; }
}

public class GetLowStockRequest
{
    public Guid? TheaterId { get; set; }
}

public class LookupPickupRequest
{
    public Guid? TheaterId { get; set; }

    /// <summary>The invoice code the customer shows.</summary>
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;
}

/// <summary>A tracked item at or under its low-stock threshold. Also the <c>StockLow</c> hub event payload.</summary>
public class LowStockItemDTO
{
    public Guid FoodAndDrinkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int LowStockThreshold { get; set; }
    public int TargetStockLevel { get; set; }
}

/// <summary>The <c>FoodOrderUpdated</c> hub event payload.</summary>
public class FoodOrderUpdateDTO
{
    public Guid InvoiceId { get; set; }
    public Guid TheaterId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public FoodOrderStatus FoodStatus { get; set; }
    public DateTime? FoodHandedOverAt { get; set; }
}
