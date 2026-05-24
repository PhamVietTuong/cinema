namespace Cinema.Business.DTO.Movies;
public class CommentDTO
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatar { get; set; }
    public Guid? ParentId { get; set; }
    public DateTime CreationTime { get; set; }
    public List<CommentDTO> Replies { get; set; } = new();
}
