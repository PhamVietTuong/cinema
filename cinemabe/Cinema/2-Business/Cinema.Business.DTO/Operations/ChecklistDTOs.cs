using System.ComponentModel.DataAnnotations;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Operations;

public class GetChecklistTemplatesRequest
{
    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }
}

public class ChecklistTemplateItemDTO
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(300)]
    public string Text { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
}

public class ChecklistTemplateDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ChecklistKind Kind { get; set; }
    public bool IsActive { get; set; }
    public List<ChecklistTemplateItemDTO> Items { get; set; } = new();
}

public class SaveChecklistTemplateRequest
{
    /// <summary>Null creates a template; set it to edit one (the items are replaced by <see cref="Items"/>).</summary>
    public Guid? Id { get; set; }

    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    public ChecklistKind Kind { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(50)]
    public List<ChecklistTemplateItemDTO> Items { get; set; } = new();
}

public class OpenChecklistRequest
{
    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }
    public Guid ShowTimeId { get; set; }
    public Guid RoomId { get; set; }
    public ChecklistKind Kind { get; set; }
}

public class SetChecklistItemRequest
{
    public Guid RunItemId { get; set; }
    public bool IsDone { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public class CompleteChecklistRequest
{
    public Guid RunId { get; set; }
}

public class ChecklistRunItemDTO
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsDone { get; set; }
    public Guid? DoneByUserId { get; set; }
    public string? DoneByName { get; set; }
    public DateTime? DoneAt { get; set; }
    public string? Note { get; set; }
}

public class ChecklistRunDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public Guid ShowTimeId { get; set; }
    public Guid RoomId { get; set; }
    public ChecklistKind Kind { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public List<ChecklistRunItemDTO> Items { get; set; } = new();
}
