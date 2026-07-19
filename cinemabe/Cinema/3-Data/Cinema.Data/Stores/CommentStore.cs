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
}
