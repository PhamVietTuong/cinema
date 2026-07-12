using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Auth;
public class UpdateUserRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public Guid UserTypeId { get; set; }
    public Guid? TheaterId { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
}
