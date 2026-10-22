using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Staff;

public class SetOverridePinRequest
{
    /// <summary>4 to 8 digits.</summary>
    [Required]
    [StringLength(20)]
    public string Pin { get; set; } = string.Empty;
}
