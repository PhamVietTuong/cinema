namespace Cinema.Data.Entities;
public class UserType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<User> Users { get; set; } = new List<User>();
}
