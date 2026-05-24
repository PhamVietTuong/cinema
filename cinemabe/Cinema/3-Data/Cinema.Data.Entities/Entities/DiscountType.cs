namespace Cinema.Data.Entities;
public class DiscountType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<Discount> Discounts { get; set; } = new List<Discount>();
}
