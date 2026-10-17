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

/// <summary>The rate that actually applies to a theater, resolved from its override or the global default.</summary>
public class EffectiveLoyaltyRateDTO
{
    public int PointsPerUnit { get; set; }
    public int PointValueVnd { get; set; }
    public bool IsOverride { get; set; }
}

/// <summary>Create/update a loyalty rate row. TheaterId null saves the global default; set, saves that theater's override.</summary>
public class SaveLoyaltySettingRequest
{
    public Guid? TheaterId { get; set; }

    [Range(1, int.MaxValue)]
    public int PointsPerUnit { get; set; }

    [Range(1, int.MaxValue)]
    public int PointValueVnd { get; set; }
}
