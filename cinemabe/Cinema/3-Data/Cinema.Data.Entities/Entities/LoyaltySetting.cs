namespace Cinema.Data.Entities;

/// <summary>
/// Loyalty point conversion rate of one theater: how many VND spent earns 1 point, and how many VND
/// 1 point is worth when redeemed. There is no global rate — a theater without a row uses the built-in
/// defaults (1,000 VND = 1 point; 100 points = 1,000 VND).
/// </summary>
public class LoyaltySetting : BaseEntity
{
    public Guid TheaterId { get; set; }
    public int PointsPerUnit { get; set; }
    public int PointValueVnd { get; set; }
}
