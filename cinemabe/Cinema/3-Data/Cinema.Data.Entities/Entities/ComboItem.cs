namespace Cinema.Data.Entities;

/// <summary>One component line of a combo: <see cref="Quantity"/> units of <see cref="Component"/> per combo sold.</summary>
public class ComboItem : BaseEntity
{
    public Guid ComboId { get; set; }
    public FoodAndDrink Combo { get; set; } = null!;
    public Guid ComponentId { get; set; }
    public FoodAndDrink Component { get; set; } = null!;
    public int Quantity { get; set; } = 1;
}
