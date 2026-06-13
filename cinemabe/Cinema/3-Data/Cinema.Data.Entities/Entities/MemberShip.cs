namespace Cinema.Data.Entities;
public class MemberShip : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int MinPoints { get; set; }
    public int MaxPoints { get; set; }
    public double DiscountPercent { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
}
