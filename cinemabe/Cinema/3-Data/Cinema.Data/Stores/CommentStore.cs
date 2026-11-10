using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Data.Stores;

public class CommentStore : GenericStore<Comment>, ICommentStore
{
    public CommentStore(CinemaContext db) : base(db)
    {
    }

    public async Task<(IEnumerable<Comment> Items, int Total)> GetForModerationAsync(bool? approved, Guid? movieId, int page, int pageSize)
    {
        var query = DbSet.Include(c => c.Movie).Include(c => c.User).AsQueryable();
        if (approved.HasValue)
        {
            query = query.Where(c => c.IsApproved == approved.Value);
        }
        if (movieId.HasValue)
        {
            query = query.Where(c => c.MovieId == movieId.Value);
        }
        query = query.OrderByDescending(c => c.CreationTime);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<IEnumerable<Comment>> GetRepliesAsync(Guid parentId)
        => await DbSet.Where(c => c.ParentId == parentId).ToListAsync();

    public async Task<IReadOnlyList<CommentView>> GetRecentForMovieAsync(Guid movieId, int take)
        => await TopLevelApproved(movieId)
            .OrderByDescending(c => c.CreationTime)
            .Take(take)
            .Select(ToView)
            .ToListAsync();

    public async Task<(IReadOnlyList<CommentView> Items, int Total)> GetPagedForMovieAsync(Guid movieId, int page, int pageSize)
    {
        var query = TopLevelApproved(movieId);
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.CreationTime)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToView)
            .ToListAsync();
        return (items, total);
    }

    public async Task<int> CountApprovedForMovieAsync(Guid movieId)
        => await DbSet.AsNoTracking().CountAsync(c => c.MovieId == movieId && c.IsApproved);

    private IQueryable<Comment> TopLevelApproved(Guid movieId)
        => DbSet.AsNoTracking().Where(c => c.MovieId == movieId && c.ParentId == null && c.IsApproved);

    private static readonly System.Linq.Expressions.Expression<Func<Comment, CommentView>> ToView = c => new CommentView(
        c.Id,
        c.Content,
        c.ParentId,
        c.UserId,
        c.CreationTime,
        c.User.Name,
        c.User.Avatar,
        c.Replies
            .Where(r => r.IsApproved)
            .OrderBy(r => r.CreationTime)
            .Select(r => new CommentView(
                r.Id, r.Content, r.ParentId, r.UserId, r.CreationTime,
                r.User.Name, r.User.Avatar, new List<CommentView>()))
            .ToList());
}
