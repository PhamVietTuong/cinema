using System.ComponentModel.DataAnnotations;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Staff;

/// <summary>
/// Manager approval attached to a sensitive staff request: the approver picks themselves from
/// <c>GetOverrideApprovers</c> and types their PIN at the terminal. Never cached client-side.
/// </summary>
public class ManagerOverrideDTO
{
    public Guid ApproverUserId { get; set; }

    [Required]
    [StringLength(20)]
    public string Pin { get; set; } = string.Empty;
}

public class SetOverridePinRequest
{
    /// <summary>4 to 8 digits.</summary>
    [Required]
    [StringLength(20)]
    public string Pin { get; set; } = string.Empty;
}

public class OverrideApproversRequest
{
    /// <summary>Required for admins; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }
}

public class OverrideApproverDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

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
