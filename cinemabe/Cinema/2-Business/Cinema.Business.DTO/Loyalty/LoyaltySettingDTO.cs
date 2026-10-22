using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Loyalty;

/// <summary>A loyalty rate row: the global default (TheaterId null) or one theater's override.</summary>
public class LoyaltySettingDTO
{
    public Guid? TheaterId { get; set; }
    public int PointsPerUnit { get; set; }
    public int PointValueVnd { get; set; }
    public DateTime? LastUpdatedTime { get; set; }
}
