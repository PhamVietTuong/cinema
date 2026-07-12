using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Auth;
public class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public Guid UserTypeId { get; set; }
    /// <summary>Required for theater-staff accounts; the theater they manage.</summary>
    public Guid? TheaterId { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
}
