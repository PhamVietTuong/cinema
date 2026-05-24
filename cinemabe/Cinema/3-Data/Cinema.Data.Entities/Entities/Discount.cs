namespace Cinema.Data.Entities;
public class Discount : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Percent { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public Guid DiscountTypeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? MaxUsage { get; set; }
    public int UsedCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DiscountType DiscountType { get; set; } = null!;
}
