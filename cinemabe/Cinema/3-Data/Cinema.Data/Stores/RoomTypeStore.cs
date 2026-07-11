using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class RoomTypeStore : GenericStore<RoomType>, IRoomTypeStore
{
    public RoomTypeStore(CinemaContext db) : base(db)
    {
    }
}
