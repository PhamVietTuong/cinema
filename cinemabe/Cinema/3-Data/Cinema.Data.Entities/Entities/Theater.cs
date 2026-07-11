namespace Cinema.Data.Entities;
public class Theater : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}
