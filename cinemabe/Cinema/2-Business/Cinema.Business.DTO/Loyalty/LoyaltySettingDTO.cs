
namespace Cinema.Business.DTO.Loyalty;

/// <summary>A theater's loyalty rate row.</summary>
public class LoyaltySettingDTO
{
    public Guid TheaterId { get; set; }
    public int PointsPerUnit { get; set; }
    public int PointValueVnd { get; set; }
    public DateTime? LastUpdatedTime { get; set; }
}
