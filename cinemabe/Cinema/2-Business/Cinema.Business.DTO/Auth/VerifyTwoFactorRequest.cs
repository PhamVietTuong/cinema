using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Auth;
public class VerifyTwoFactorRequest
{
    [Required]
    [StringLength(256, MinimumLength = 3)]
    public string EmailOrPhone { get; set; } = string.Empty;

    // IssueTwoFactorCodeAsync generates a 6-digit numeric code.
    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "The code must be 6 digits.")]
    public string Code { get; set; } = string.Empty;
}
