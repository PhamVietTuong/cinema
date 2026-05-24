namespace Cinema.Data.Entities;
public class FoodAndDrinkTheater
{
    public Guid FoodAndDrinkId { get; set; }
    public Guid TheaterId { get; set; }
    public bool IsAvailable { get; set; } = true;
    public FoodAndDrink FoodAndDrink { get; set; } = null!;
    public Theater Theater { get; set; } = null!;
}
