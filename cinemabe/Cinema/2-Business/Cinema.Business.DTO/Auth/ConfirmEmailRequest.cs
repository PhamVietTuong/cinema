using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Auth;
public class ConfirmEmailRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(512)]
    public string Token { get; set; } = string.Empty;
}
