using Cinema.Data.Enums;

namespace Cinema.Business.DTO;
public class UserDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public Guid UserTypeId { get; set; }
    public string UserTypeName { get; set; } = string.Empty;
    public Guid? TheaterId { get; set; }
    public int Points { get; set; }
    public bool NotifyBookingEmails { get; set; }
    public bool NotifyPromotionEmails { get; set; }
    public bool NotifyReminderEmails { get; set; }
    public string? MemberShipName { get; set; }
    public UserStatus Status { get; set; }
    public DateTime CreationTime { get; set; }
}
