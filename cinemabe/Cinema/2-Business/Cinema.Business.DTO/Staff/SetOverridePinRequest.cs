using System.ComponentModel.DataAnnotations;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Staff;

public class SetOverridePinRequest
{
    /// <summary>4 to 8 digits.</summary>
    [Required]
    [StringLength(20)]
    public string Pin { get; set; } = string.Empty;
}
