using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Staff;

public class AuditLogDTO
{
    public Guid Id { get; set; }
    public Guid? TheaterId { get; set; }
    public Guid ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public Guid? ApproverUserId { get; set; }
    public string? ApproverName { get; set; }
    public AuditAction Action { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public double? Amount { get; set; }
    public StaffReasonCode? ReasonCode { get; set; }
    public string? Reason { get; set; }
    public string? DataJson { get; set; }
    public DateTime CreationTime { get; set; }
}
