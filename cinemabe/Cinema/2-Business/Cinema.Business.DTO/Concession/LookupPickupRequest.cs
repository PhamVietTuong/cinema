using System.ComponentModel.DataAnnotations;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Concession;

public class LookupPickupRequest
{
    public Guid? TheaterId { get; set; }

    /// <summary>The invoice code the customer shows.</summary>
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;
}
