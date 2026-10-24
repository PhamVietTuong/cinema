using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class MovieCreditStore : GenericStore<MovieCredit>, IMovieCreditStore
{
    public MovieCreditStore(CinemaContext db) : base(db)
    {
    }

    public void TrackNew(MovieCredit credit)
    {
        Context.Add(credit);
    }

    public async Task<IReadOnlyList<MovieCreditRow>> GetByMovieAsync(Guid movieId)
    {
        return await DbSet
            .AsNoTracking()
            .Where(c => c.MovieId == movieId)
            .OrderBy(c => c.CreditType).ThenBy(c => c.DisplayOrder).ThenBy(c => c.Person.Name)
            .Select(c => new MovieCreditRow(
                c.Id,
                c.PersonId,
                c.Person.Name,
                c.Person.PhotoUrl,
                c.CreditType,
                c.CharacterName,
                c.DisplayOrder))
            .ToListAsync();
    }
}
