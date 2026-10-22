using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>The few columns the pickup status transition needs.</summary>
public sealed class FoodOrderHeaderRow
{
    public Guid InvoiceId { get; set; }
    public Guid TheaterId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public InvoiceStatus InvoiceStatus { get; set; }
    public FoodOrderStatus FoodStatus { get; set; }
}
