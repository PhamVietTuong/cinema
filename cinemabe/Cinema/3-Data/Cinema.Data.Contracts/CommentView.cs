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
