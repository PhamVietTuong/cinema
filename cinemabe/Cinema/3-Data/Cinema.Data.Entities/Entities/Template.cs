using Cinema.Data.Enums;
namespace Cinema.Data.Entities;

/// <summary>A notification template (email or SMS) with {{Token}} placeholders, resolved by
/// TemplateRenderer when a notification is sent. Code is the lookup key used by the sending code
/// (e.g. BookingManager) and is immutable after creation.</summary>
public class Template : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TemplateChannel Channel { get; set; } = TemplateChannel.Email;
    /// <summary>Email only; null for SMS templates.</summary>
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
