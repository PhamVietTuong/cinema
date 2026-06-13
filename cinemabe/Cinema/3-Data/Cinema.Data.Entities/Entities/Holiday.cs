namespace Cinema.Data.Entities;
public class Holiday : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public double PriceMultiplier { get; set; } = 1.5;
}
