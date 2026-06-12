using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Data.Stores;

public class CommentStore : GenericStore<Comment>, ICommentStore
{
    public CommentStore(CinemaContext db) : base(db)
    {
    }
}
