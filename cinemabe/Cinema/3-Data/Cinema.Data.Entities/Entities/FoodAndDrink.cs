namespace Cinema.Data.Entities;
public class FoodAndDrink : BaseEntity
{
    /// <summary>The theater this item belongs to (food &amp; drinks are per-theater).</summary>
    public Guid TheaterId { get; set; }
    public Theater Theater { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; } = true;
    public ICollection<InvoiceFoodAndDrink> InvoiceFoodAndDrinks { get; set; } = new List<InvoiceFoodAndDrink>();
}
