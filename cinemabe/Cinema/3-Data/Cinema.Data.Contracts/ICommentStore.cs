using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

/// <summary>
/// Read model for rendering a comment thread. Deliberately narrow: projecting to this keeps the
/// commenter's credential columns (PasswordHash / PasswordSalt / reset + 2FA token hashes) out of a
/// query that exists only to render a public page.
/// </summary>
public record CommentView(
    Guid Id,
    string Content,
    Guid? ParentId,
    DateTime CreationTime,
    string UserName,
    string? UserAvatar,
    IReadOnlyList<CommentView> Replies);

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
