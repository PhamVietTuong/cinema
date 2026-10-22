using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Staff;

/// <summary>What <c>IAuditLogger</c> records. <c>ApproverUserId</c> is the manager who authorised the action.</summary>
public class AuditEntry
{
    public Guid? TheaterId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid? ApproverUserId { get; set; }
    public AuditAction Action { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public double? Amount { get; set; }
    public StaffReasonCode? ReasonCode { get; set; }
    public string? Reason { get; set; }
    public string? DataJson { get; set; }
}
