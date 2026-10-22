using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Concession;

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
