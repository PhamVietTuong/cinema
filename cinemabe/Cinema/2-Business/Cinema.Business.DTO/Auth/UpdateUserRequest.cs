using System.ComponentModel.DataAnnotations;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Auth;
public class UpdateUserRequest
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(20, MinimumLength = 8)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(512)]
    public string? Avatar { get; set; }

    [Required]
    public Guid UserTypeId { get; set; }

    public Guid? TheaterId { get; set; }

    [EnumDataType(typeof(UserStatus))]
    public UserStatus Status { get; set; } = UserStatus.Active;
}
