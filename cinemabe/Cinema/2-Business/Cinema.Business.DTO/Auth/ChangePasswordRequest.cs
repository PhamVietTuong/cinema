using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Auth;
public class ChangePasswordRequest
{
    [Required]
    [StringLength(100)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
