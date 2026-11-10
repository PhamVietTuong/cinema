using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Loyalty;

/// <summary>Create/update a theater's loyalty rate.</summary>
public class SaveLoyaltySettingRequest
{
    [Required]
    public Guid TheaterId { get; set; }

    [Range(1, int.MaxValue)]
    public int PointsPerUnit { get; set; }

    [Range(1, int.MaxValue)]
    public int PointValueVnd { get; set; }
}
