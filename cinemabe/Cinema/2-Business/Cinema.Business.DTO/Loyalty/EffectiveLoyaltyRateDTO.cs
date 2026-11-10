
namespace Cinema.Business.DTO.Loyalty;

/// <summary>The rate that actually applies to a theater, resolved from its own setting or the built-in default.</summary>
public class EffectiveLoyaltyRateDTO
{
    public int PointsPerUnit { get; set; }
    public int PointValueVnd { get; set; }
    public bool IsCustom { get; set; }
}
