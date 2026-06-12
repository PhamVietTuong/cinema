using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class MovieTypeDetailStore : IMovieTypeDetailStore
{
    private readonly CinemaContext _db;

    public MovieTypeDetailStore(CinemaContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<MovieTypeDetail>> GetAllAsync()
    {
        return await _db.Set<MovieTypeDetail>()
            .Include(x => x.Movie)
            .Include(x => x.MovieType)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(Guid movieId, Guid movieTypeId)
    {
        return await _db.Set<MovieTypeDetail>()
            .AnyAsync(x => x.MovieId == movieId && x.MovieTypeId == movieTypeId);
    }

    public async Task AddAsync(MovieTypeDetail entity)
    {
        _db.Set<MovieTypeDetail>().Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid movieId, Guid movieTypeId)
    {
        var entity = await _db.Set<MovieTypeDetail>()
            .FirstOrDefaultAsync(x => x.MovieId == movieId && x.MovieTypeId == movieTypeId);
        if (entity != null)
        {
            _db.Set<MovieTypeDetail>().Remove(entity);
            await _db.SaveChangesAsync();
        }
    }
}
