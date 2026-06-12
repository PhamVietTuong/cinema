using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class NewsStore : GenericStore<News>, INewsStore
{
    public NewsStore(CinemaContext db) : base(db)
    {
    }
}
