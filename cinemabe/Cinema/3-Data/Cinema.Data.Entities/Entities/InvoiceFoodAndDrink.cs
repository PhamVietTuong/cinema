namespace Cinema.Data.Entities;
public class InvoiceFoodAndDrink
{
    public Guid InvoiceId { get; set; }
    public Guid FoodAndDrinkId { get; set; }
    public int Quantity { get; set; }
    public double UnitPrice { get; set; }
    public double TotalPrice { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public FoodAndDrink FoodAndDrink { get; set; } = null!;
}
