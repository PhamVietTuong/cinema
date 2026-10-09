using Cinema.Data.Enums;

namespace Cinema.Data.Entities;

/// <summary>A theater's reusable checklist (e.g. pre-show room check). At most one active template per theater and kind.</summary>
public class ChecklistTemplate : BaseEntity
{
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ChecklistKind Kind { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ChecklistTemplateItem> Items { get; set; } = new List<ChecklistTemplateItem>();
}

public class ChecklistTemplateItem : BaseEntity
{
    public Guid ChecklistTemplateId { get; set; }
    public int SortOrder { get; set; }
    public string Text { get; set; } = string.Empty;
    /// <summary>A required item must be ticked before the run can be completed.</summary>
    public bool IsRequired { get; set; } = true;
    public ChecklistTemplate ChecklistTemplate { get; set; } = null!;
}

/// <summary>
/// One showtime's checklist, created lazily the first time someone opens it. It copies the template's items, so
/// later template edits never change a run that is already in progress or done. No FK to the template or showtime.
/// </summary>
public class ChecklistRun : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Guid ShowTimeId { get; set; }
    public Guid RoomId { get; set; }
    public ChecklistKind Kind { get; set; }
    public Guid ChecklistTemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public ICollection<ChecklistRunItem> Items { get; set; } = new List<ChecklistRunItem>();
}

public class ChecklistRunItem : BaseEntity
{
    public Guid ChecklistRunId { get; set; }
    public int SortOrder { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public bool IsDone { get; set; }
    public Guid? DoneByUserId { get; set; }
    public DateTime? DoneAt { get; set; }
    public string? Note { get; set; }
    public ChecklistRun ChecklistRun { get; set; } = null!;
}
