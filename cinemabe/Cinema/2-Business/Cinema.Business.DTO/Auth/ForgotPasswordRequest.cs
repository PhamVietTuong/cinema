using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Auth;
public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;
}
