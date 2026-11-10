namespace Cinema.Business.DTO.Movies;
public class CommentDTO
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatar { get; set; }
    public Guid? ParentId { get; set; }
    /// <summary>Author of the comment.</summary>
    public Guid UserId { get; set; }
    /// <summary>True when the requesting (signed-in) user wrote this comment, so the UI can offer edit/delete.</summary>
    public bool IsMine { get; set; }
    public DateTime CreationTime { get; set; }
    public List<CommentDTO> Replies { get; set; } = new();
}
