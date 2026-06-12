using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class RoomStore : GenericStore<Room>, IRoomStore
{
    public RoomStore(CinemaContext db) : base(db)
    {
    }
}
