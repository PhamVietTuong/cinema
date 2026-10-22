using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Loyalty;

/// <summary>Create/update a loyalty rate row. TheaterId null saves the global default; set, saves that theater's override.</summary>
public class SaveLoyaltySettingRequest
{
    public Guid? TheaterId { get; set; }

    [Range(1, int.MaxValue)]
    public int PointsPerUnit { get; set; }

    [Range(1, int.MaxValue)]
    public int PointValueVnd { get; set; }
}
