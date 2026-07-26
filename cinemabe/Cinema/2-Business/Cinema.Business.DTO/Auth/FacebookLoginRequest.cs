using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Auth;
public class FacebookLoginRequest
{
    [Required]
    [StringLength(4096)]
    public string AccessToken { get; set; } = string.Empty;
}
