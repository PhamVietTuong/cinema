using Cinema.Data.Enums;

namespace Cinema.Data.Entities;

/// <summary>A theater's restock plan: what to bring into the warehouse, approved before it is received.</summary>
public class StoragePlan : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Guid TheaterId { get; set; }
    public Theater Theater { get; set; } = null!;
    public StoragePlanStatus Status { get; set; } = StoragePlanStatus.Draft;
    public DateTime TargetDate { get; set; }
    public string? Supplier { get; set; }
    public string? Note { get; set; }

    // User ids carry no FK so the plan survives user deletion and avoids multiple cascade paths.
    public Guid CreatedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? ReceivedByUserId { get; set; }
    public DateTime? ReceivedAt { get; set; }

    /// <summary>Optimistic-concurrency token: stops two people changing (or receiving) the same plan at once.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<StoragePlanItem> Items { get; set; } = new List<StoragePlanItem>();
}
