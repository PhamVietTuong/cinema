using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Auth;
public class GoogleLoginRequest
{
    [Required]
    [StringLength(4096)]
    public string IdToken { get; set; } = string.Empty;
}
