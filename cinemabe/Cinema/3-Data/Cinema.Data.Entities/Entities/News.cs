namespace Cinema.Data.Entities;
public class News : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? Author { get; set; }
    public bool IsPublished { get; set; } = false;
    public DateTime? PublishedAt { get; set; }
}
