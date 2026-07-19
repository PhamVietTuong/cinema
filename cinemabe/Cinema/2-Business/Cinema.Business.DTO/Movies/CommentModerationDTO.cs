namespace Cinema.Business.DTO.Movies;

/// <summary>A comment as seen by an admin moderating user-submitted content.</summary>
public class CommentModerationDTO
{
    public Guid Id { get; set; }
    public Guid MovieId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public Guid? ParentId { get; set; }
    public DateTime CreationTime { get; set; }
}
