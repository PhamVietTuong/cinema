using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class MovieStore : GenericStore<Movie>, IMovieStore
{
    public MovieStore(CinemaContext db) : base(db) { }

    public async Task<(IEnumerable<Movie> Items, int Total)> GetPagedAsync(MovieSearchFilter filter, int page, int pageSize)
    {
        var q = DbSet
            .AsNoTracking()
            .Include(m => m.AgeRestriction)
            .Include(m => m.MovieTypeDetails).ThenInclude(mt => mt.MovieType)
            .Where(m => m.IsActive);

        var search = filter.Search;
        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(m => m.Title.Contains(search) || (m.Director != null && m.Director.Contains(search)) || (m.Cast != null && m.Cast.Contains(search)));
        }

        var director = filter.Director;
        if (!string.IsNullOrWhiteSpace(director))
        {
            q = q.Where(m => m.Director != null && m.Director.Contains(director));
        }

        if (filter.MovieTypeId.HasValue)
        {
            var typeId = filter.MovieTypeId.Value;
            q = q.Where(m => m.MovieTypeDetails.Any(mt => mt.MovieTypeId == typeId));
        }

        var language = filter.Language;
        if (!string.IsNullOrWhiteSpace(language))
        {
            q = q.Where(m => m.Language == language);
        }

        if (filter.TheaterId.HasValue || filter.ShowFrom.HasValue || filter.ShowTo.HasValue)
        {
            var theaterId = filter.TheaterId;
            var from = filter.ShowFrom;
            var to = filter.ShowTo;
            q = q.Where(m => m.ShowTimes.Any(s => s.IsActive
                && (from == null || s.StartTime >= from)
                && (to == null || s.StartTime < to)
                && (theaterId == null || s.ShowTimeRooms.Any(sr => sr.Room.TheaterId == theaterId))));
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var coming = filter.Status == "coming";
        if (filter.Status == "now")
        {
            q = q.Where(m => m.ReleaseDate <= today && (m.EndDate == null || m.EndDate >= today));
        }
        else if (coming)
        {
            q = q.Where(m => m.ReleaseDate > today);
        }

        var total = await q.CountAsync();
        var ordered = coming ? q.OrderBy(m => m.ReleaseDate) : q.OrderByDescending(m => m.ReleaseDate);
        var items = await ordered.ThenBy(m => m.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<IReadOnlyList<string>> GetLanguagesAsync()
        => await DbSet
            .AsNoTracking()
            .Where(m => m.IsActive && m.Language != null && m.Language != "")
            .Select(m => m.Language!)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync();

    public async Task<int> GetRatingCountAsync(Guid movieId)
        => await Context.Evaluation.CountAsync(e => e.MovieId == movieId);

    // Comments are deliberately NOT included here: they are loaded separately (and bounded) by
    // ICommentStore.GetRecentForMovieAsync. Joining them in multiplied every showtime row by every
    // comment row by every evaluation row, and dragged each commenter's full User record — password
    // hash and reset tokens included — into memory just to render a public page.
    // ShowTimes are likewise NOT included here: the detail page only needs a bounded (today + next 3
    // days) schedule window, loaded separately and untracked by IShowTimeStore.GetMovieScheduleAsync.
    // Including every future ShowTime x Room here made this an unbounded query that grew with every
    // theater/showtime added to the movie.
    // AsSplitQuery keeps the remaining collections from cross-joining each other.
    public async Task<Movie?> GetDetailAsync(Guid id)
        => await DbSet
            .Include(m => m.AgeRestriction)
            .Include(m => m.MovieTypeDetails).ThenInclude(mt => mt.MovieType)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id);

    public async Task<Movie?> GetDetailBySlugAsync(string slug)
        => await DbSet
            .AsNoTracking()
            .Include(m => m.AgeRestriction)
            .Include(m => m.MovieTypeDetails).ThenInclude(mt => mt.MovieType)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Slug == slug);

    public async Task<Movie?> GetForUpdateAsync(Guid id)
    {
        // MovieTypeDetails and MovieCredits are independent collections: including both in one query
        // produces a cartesian result (rows = genres x credits), per this repo's own guidance against
        // including a collection navigation alongside other includes. Load the credits in a second
        // query instead; EF tracks and attaches them to the same Movie instance via the FK, since both
        // come from this store's single DbContext.
        var movie = await DbSet
            .Include(m => m.MovieTypeDetails)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (movie != null)
        {
            await Context.Set<MovieCredit>().Where(c => c.MovieId == id).LoadAsync();
        }
        return movie;
    }

    public async Task<IEnumerable<Movie>> GetNowShowingAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return await DbSet
            .Include(m => m.AgeRestriction)
            .Include(m => m.MovieTypeDetails).ThenInclude(mt => mt.MovieType)
            .Include(m => m.Evaluations)
            .Where(m => m.IsActive && m.ReleaseDate <= today && (m.EndDate == null || m.EndDate >= today))
            .OrderByDescending(m => m.ReleaseDate)
            // Genres and evaluations are independent collections: in one query each movie's rows
            // become genres x ratings, which grows with every rating a film receives.
            .AsSplitQuery()
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
        // Averaged in SQL: pulling every score back to average in memory scales with the
        // number of ratings, which for a popular film is thousands of rows per page render.
        => await Context.Evaluation
            .Where(e => e.MovieId == movieId)
            .Select(e => (double?)e.Score)
            .AverageAsync() ?? 0;

    public async Task<IReadOnlyDictionary<Guid, double>> GetAverageRatingsAsync(IReadOnlyCollection<Guid> movieIds)
    {
        if (movieIds.Count == 0)
        {
            return new Dictionary<Guid, double>();
        }

        var rows = await Context.Evaluation
            .Where(e => movieIds.Contains(e.MovieId))
            .GroupBy(e => e.MovieId)
            .Select(g => new { MovieId = g.Key, Average = g.Average(e => (double)e.Score) })
            .ToListAsync();

        return rows.ToDictionary(r => r.MovieId, r => r.Average);
    }

    public async Task<IReadOnlyList<string>> GetSlugsStartingWithAsync(string baseSlug)
        => await DbSet
            .AsNoTracking()
            .Where(m => m.Slug == baseSlug || m.Slug.StartsWith(baseSlug + "-"))
            .Select(m => m.Slug)
            .ToListAsync();
}
