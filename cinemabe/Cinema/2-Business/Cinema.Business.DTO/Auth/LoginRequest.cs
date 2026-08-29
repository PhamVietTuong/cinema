using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Auth;
public class LoginRequest
{
    [Required]
    [StringLength(256, MinimumLength = 3)]
    public string EmailOrPhone { get; set; } = string.Empty;

    // Length policy is deliberately not enforced here — only on register/change/reset. A minimum on
    // login would 400 (rather than 401) for legacy passwords and leak the policy to anonymous callers.
    [Required]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;
}
