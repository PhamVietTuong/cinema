using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface ICommentStore : IGenericStore<Comment>
{
    /// <summary>Paged comments for admin moderation (with Movie + User), optionally filtered by
    /// approval state and movie, newest first.</summary>
    Task<(IEnumerable<Comment> Items, int Total)> GetForModerationAsync(bool? approved, Guid? movieId, int page, int pageSize);

    /// <summary>All direct replies to a comment (used when deleting a parent so no reply is orphaned).</summary>
    Task<IEnumerable<Comment>> GetRepliesAsync(Guid parentId);

    /// <summary>Newest approved top-level comments for a movie (with their approved replies), capped at
    /// <paramref name="take"/>. Kept off the movie-detail query so comments don't multiply its other joins.</summary>
    Task<IReadOnlyList<CommentView>> GetRecentForMovieAsync(Guid movieId, int take);
}
