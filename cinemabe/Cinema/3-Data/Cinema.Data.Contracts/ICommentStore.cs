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

    /// <summary>One page of approved top-level comments for a movie (newest first, each with its approved
    /// replies) plus the total number of top-level comments.</summary>
    Task<(IReadOnlyList<CommentView> Items, int Total)> GetPagedForMovieAsync(Guid movieId, int page, int pageSize);

    /// <summary>Count of approved comments for a movie, replies included.</summary>
    Task<int> CountApprovedForMovieAsync(Guid movieId);
}
