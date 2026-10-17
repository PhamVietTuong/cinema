namespace Cinema.Data.Entities;

/// <summary>
/// Loyalty point conversion rate: how many VND spent earns 1 point, and how many VND 1 point is
/// worth when redeemed. <see cref="TheaterId"/> null is the global default row; a row with
/// <see cref="TheaterId"/> set overrides the rate for that one theater.
/// </summary>
public class LoyaltySetting : BaseEntity
{
    public Guid? TheaterId { get; set; }
    public int PointsPerUnit { get; set; }
    public int PointValueVnd { get; set; }
}
