using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class UserTypeStore : GenericStore<UserType>, IUserTypeStore
{
    public UserTypeStore(CinemaContext db) : base(db)
    {
    }
}
