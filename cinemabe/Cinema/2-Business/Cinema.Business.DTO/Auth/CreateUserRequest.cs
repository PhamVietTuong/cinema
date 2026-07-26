using System.ComponentModel.DataAnnotations;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Auth;
public class CreateUserRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(20, MinimumLength = 8)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public Guid UserTypeId { get; set; }

    /// <summary>Required for theater-staff accounts; the theater they manage.</summary>
    public Guid? TheaterId { get; set; }

    [EnumDataType(typeof(UserStatus))]
    public UserStatus Status { get; set; } = UserStatus.Active;
}
