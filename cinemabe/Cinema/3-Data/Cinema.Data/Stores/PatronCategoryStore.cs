using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class PatronCategoryStore : GenericStore<PatronCategory>, IPatronCategoryStore
{
    public PatronCategoryStore(CinemaContext db) : base(db)
    {
    }
}
