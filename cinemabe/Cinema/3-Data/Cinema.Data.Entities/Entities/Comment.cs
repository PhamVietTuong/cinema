namespace Cinema.Data.Entities;
public class Comment : BaseEntity
{
    public Guid MovieId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public bool IsApproved { get; set; } = true;
    public Movie Movie { get; set; } = null!;
    public User User { get; set; } = null!;
    public Comment? Parent { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();
}
