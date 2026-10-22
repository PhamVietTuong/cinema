using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Concession;

/// <summary>The <c>FoodOrderUpdated</c> hub event payload.</summary>
public class FoodOrderUpdateDTO
{
    public Guid InvoiceId { get; set; }
    public Guid TheaterId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public FoodOrderStatus FoodStatus { get; set; }
    public DateTime? FoodHandedOverAt { get; set; }
}
