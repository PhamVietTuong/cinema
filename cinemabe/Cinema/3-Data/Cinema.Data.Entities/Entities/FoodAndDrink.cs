namespace Cinema.Data.Entities;
public class FoodAndDrink : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; } = true;
    public ICollection<FoodAndDrinkTheater> FoodAndDrinkTheaters { get; set; } = new List<FoodAndDrinkTheater>();
    public ICollection<InvoiceFoodAndDrink> InvoiceFoodAndDrinks { get; set; } = new List<InvoiceFoodAndDrink>();
}
