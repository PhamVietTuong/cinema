using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores
{
    public class UserStore : GenericStore<User>, IUserStore
    {
        public UserStore(CinemaContext cinemaContext) : base(cinemaContext)
        {
        }
    }
}
