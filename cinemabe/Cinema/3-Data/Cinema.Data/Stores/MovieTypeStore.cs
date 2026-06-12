using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class MovieTypeStore : GenericStore<MovieType>, IMovieTypeStore
{
    public MovieTypeStore(CinemaContext db) : base(db)
    {
    }
}
