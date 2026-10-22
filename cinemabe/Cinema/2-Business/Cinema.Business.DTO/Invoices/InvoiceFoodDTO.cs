
namespace Cinema.Business.DTO.Invoices;

public class InvoiceFoodDTO
{
    public string FoodName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public double UnitPrice { get; set; }
    public double TotalPrice { get; set; }
}
