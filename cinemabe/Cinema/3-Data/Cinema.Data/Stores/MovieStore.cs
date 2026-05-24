using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class MovieStore : GenericStore<Movie>, IMovieStore
{
    public MovieStore(CinemaContext db) : base(db) { }

    public async Task<(IEnumerable<Movie> Items, int Total)> GetPagedAsync(
        string? search, Guid? movieTypeId, int page, int pageSize)
    {
        var q = DbSet
            .Include(m => m.AgeRestriction)
            .Include(m => m.MovieTypeDetails).ThenInclude(mt => mt.MovieType)
            .Where(m => m.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(m => m.Title.Contains(search) || (m.Director != null && m.Director.Contains(search)) || (m.Cast != null && m.Cast.Contains(search)));

        if (movieTypeId.HasValue)
            q = q.Where(m => m.MovieTypeDetails.Any(mt => mt.MovieTypeId == movieTypeId.Value));

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(m => m.ReleaseDate)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<Movie?> GetDetailAsync(Guid id)
        => await DbSet
            .Include(m => m.AgeRestriction)
            .Include(m => m.MovieTypeDetails).ThenInclude(mt => mt.MovieType)
            .Include(m => m.ShowTimes).ThenInclude(s => s.ShowTimeRooms).ThenInclude(sr => sr.Room).ThenInclude(r => r.Theater)
            .Include(m => m.Comments.Where(c => c.ParentId == null && c.IsApproved)).ThenInclude(c => c.User)
            .Include(m => m.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.User)
            .Include(m => m.Evaluations)
            .FirstOrDefaultAsync(m => m.Id == id);

    public async Task<IEnumerable<Movie>> GetNowShowingAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return await DbSet
            .Include(m => m.AgeRestriction)
            .Include(m => m.MovieTypeDetails).ThenInclude(mt => mt.MovieType)
            .Include(m => m.Evaluations)
            .Where(m => m.IsActive && m.ReleaseDate <= today && (m.EndDate == null || m.EndDate >= today))
            .OrderByDescending(m => m.ReleaseDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Movie>> GetComingSoonAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return await DbSet
            .Include(m => m.AgeRestriction)
            .Include(m => m.MovieTypeDetails).ThenInclude(mt => mt.MovieType)
            .Where(m => m.IsActive && m.ReleaseDate > today)
            .OrderBy(m => m.ReleaseDate)
            .ToListAsync();
    }

    public async Task<double> GetAverageRatingAsync(Guid movieId)
    {
        var evals = await Context.Evaluation.Where(e => e.MovieId == movieId).Select(e => e.Score).ToListAsync();
        return evals.Count == 0 ? 0 : evals.Average();
    }
}
