using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class AgeRestrictionStore : GenericStore<AgeRestriction>, IAgeRestrictionStore
{
    public AgeRestrictionStore(CinemaContext db) : base(db)
    {
    }
}
