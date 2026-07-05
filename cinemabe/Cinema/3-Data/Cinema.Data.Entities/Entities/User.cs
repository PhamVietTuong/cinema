using Cinema.Data.Enums;
namespace Cinema.Data.Entities;
public class User : BaseEntity
{
    public new Guid Id { get; set; } = Guid.NewGuid();
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
    public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();
    public UserStatus Status { get; set; } = UserStatus.Active;
    public Guid UserTypeId { get; set; }
    public Guid? MemberShipId { get; set; }
    public int Points { get; set; } = 0;
    // Password reset: SHA-256 hash of the emailed token + its expiry (both null when no reset is pending).
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetExpiresAt { get; set; }
    public UserType UserType { get; set; } = null!;
    public MemberShip? MemberShip { get; set; }
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Evaluation> Evaluations { get; set; } = new List<Evaluation>();
}
