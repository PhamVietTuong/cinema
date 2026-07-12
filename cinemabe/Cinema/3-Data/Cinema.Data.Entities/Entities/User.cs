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
    /// <summary>For theater-staff accounts: the single theater they manage (null for admin/customer).</summary>
    public Guid? TheaterId { get; set; }
    public Guid? MemberShipId { get; set; }
    public int Points { get; set; } = 0;
    // Password reset: SHA-256 hash of the emailed token + its expiry (both null when no reset is pending).
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetExpiresAt { get; set; }
    // Account lockout: consecutive failed-login counter and the UTC time until which login is blocked.
    public int FailedLoginCount { get; set; } = 0;
    public DateTime? LockoutEndUtc { get; set; }
    // Email verification: false until the user confirms via the emailed link.
    public bool EmailConfirmed { get; set; } = false;
    public string? EmailVerificationTokenHash { get; set; }
    public DateTime? EmailVerificationExpiresAt { get; set; }
    // Two-factor auth: when enabled, a one-time code (hash + expiry) is emailed at each login.
    public bool TwoFactorEnabled { get; set; } = false;
    public string? TwoFactorCodeHash { get; set; }
    public DateTime? TwoFactorCodeExpiresAt { get; set; }
    public UserType UserType { get; set; } = null!;
    public MemberShip? MemberShip { get; set; }
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Evaluation> Evaluations { get; set; } = new List<Evaluation>();
}
