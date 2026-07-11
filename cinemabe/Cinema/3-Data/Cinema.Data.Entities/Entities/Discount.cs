namespace Cinema.Data.Entities;
public class Discount : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Percent { get; set; }
    public double? MaxDiscountAmount { get; set; }
    public Guid DiscountTypeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? MaxUsage { get; set; }
    public int UsedCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    /// <summary>Theater this code is limited to; null = applies system-wide.</summary>
    public Guid? TheaterId { get; set; }
    public DiscountType DiscountType { get; set; } = null!;
    public Theater? Theater { get; set; }
}
