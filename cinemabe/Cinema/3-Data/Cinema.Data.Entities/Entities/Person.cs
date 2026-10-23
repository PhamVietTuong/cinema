namespace Cinema.Data.Entities;
public class Person : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
}
