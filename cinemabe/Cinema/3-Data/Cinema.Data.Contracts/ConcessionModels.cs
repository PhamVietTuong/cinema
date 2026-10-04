using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>Read-only projection of one food or drink line of a pickup order.</summary>
public sealed class PickupItemRow
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

/// <summary>Read-only projection of an invoice with food, as the pickup queue and the pickup lookup show it.</summary>
public sealed class PickupOrderRow
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
    public List<PickupItemRow> Items { get; set; } = new();
}

/// <summary>The few columns the pickup status transition needs.</summary>
public sealed class FoodOrderHeaderRow
{
    public Guid InvoiceId { get; set; }
    public Guid TheaterId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public InvoiceStatus InvoiceStatus { get; set; }
    public FoodOrderStatus FoodStatus { get; set; }
}

/// <summary>Read-only projection of a tracked item at or under its low-stock threshold.</summary>
public sealed class LowStockRow
{
    public Guid FoodAndDrinkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int LowStockThreshold { get; set; }
    public int TargetStockLevel { get; set; }
}
