using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Loyalty;

/// <summary>The rate that actually applies to a theater, resolved from its override or the global default.</summary>
public class EffectiveLoyaltyRateDTO
{
    public int PointsPerUnit { get; set; }
    public int PointValueVnd { get; set; }
    public bool IsOverride { get; set; }
}
