namespace Cinema.Data.Entities;
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedTime { get; set; }
}
