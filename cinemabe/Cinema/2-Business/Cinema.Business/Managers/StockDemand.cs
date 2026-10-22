using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

// Units of one tracked item to take off the shelf for a booking, and the combos (if any) that asked for it.
internal sealed class StockDemand
{
    public StockDemand(FoodAndDrink food)
    {
        Food = food;
    }

    public FoodAndDrink Food { get; }
    public int Quantity { get; set; }
    public List<string> ComboNames { get; } = new();
}
