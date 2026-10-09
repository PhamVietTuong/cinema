using Cinema.Data.Enums;

namespace Cinema.Data.Entities;

/// <summary>
/// A customer complaint handled by theater staff. No foreign keys on purpose, like <see cref="AuditLog"/> and
/// <see cref="Incident"/>: the history survives deletion of the user/invoice it mentions.
/// </summary>
public class Complaint : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Guid? CustomerUserId { get; set; }
    public Guid? InvoiceId { get; set; }
    public ComplaintCategory Category { get; set; } = ComplaintCategory.Other;
    public string Description { get; set; } = string.Empty;
    public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;
    public ComplaintResolution Resolution { get; set; } = ComplaintResolution.None;
    /// <summary>Money (refund / gift card) or points given to the customer.</summary>
    public double? CompensationAmount { get; set; }
    /// <summary>Gift card code, refunded invoice code, or "points".</summary>
    public string? CompensationRef { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    /// <summary>Outcome note of a resolution or the reason of a rejection.</summary>
    public string? ResolutionNote { get; set; }
}
