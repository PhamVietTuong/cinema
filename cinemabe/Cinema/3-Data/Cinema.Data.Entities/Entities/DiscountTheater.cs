namespace Cinema.Data.Entities;

/// <summary>Join row scoping a <see cref="Discount"/> (promotion) to a specific theater.</summary>
public class DiscountTheater : BaseEntity
{
    public Guid DiscountId { get; set; }
    public Discount Discount { get; set; } = null!;
    public Guid TheaterId { get; set; }
    public Theater Theater { get; set; } = null!;
}
