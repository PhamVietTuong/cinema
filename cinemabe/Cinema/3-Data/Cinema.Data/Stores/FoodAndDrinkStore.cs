using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class FoodAndDrinkStore : GenericStore<FoodAndDrink>, IFoodAndDrinkStore
{
    public FoodAndDrinkStore(CinemaContext db) : base(db)
    {
    }
}
