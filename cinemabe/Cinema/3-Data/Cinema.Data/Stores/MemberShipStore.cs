using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class MemberShipStore : GenericStore<MemberShip>, IMemberShipStore
{
    public MemberShipStore(CinemaContext db) : base(db)
    {
    }
}
