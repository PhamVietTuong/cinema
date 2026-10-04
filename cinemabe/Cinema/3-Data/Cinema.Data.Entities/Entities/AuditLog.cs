using Cinema.Data.Enums;

namespace Cinema.Data.Entities;

/// <summary>
/// Insert-only audit row for a sensitive staff action. No foreign keys on purpose, so the trail survives
/// deletion of the users/theaters/entities it refers to. <see cref="ApproverUserId"/> is the manager who
/// authorised the action (equal to the actor when the actor is already an approver).
/// </summary>
public class AuditLog : BaseEntity
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
    /// <summary>Free-form JSON detail (before/after values etc.).</summary>
    public string? DataJson { get; set; }
}
