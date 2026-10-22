using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

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
