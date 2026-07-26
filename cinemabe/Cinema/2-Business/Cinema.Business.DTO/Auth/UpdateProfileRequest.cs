using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Auth;
public class UpdateProfileRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(20, MinimumLength = 8)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(512)]
    public string? Avatar { get; set; }
}
