using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>
/// Checklist templates and per-showtime runs. Methods that return tracked entities say so: edit them, then call
/// <c>IApplicationUnitOfWork.SaveChangesAsync</c>.
/// </summary>
public interface IChecklistStore
{
    /// <summary>The theater's templates with their items, items ordered by SortOrder, untracked.</summary>
    Task<List<ChecklistTemplate>> GetTemplatesAsync(Guid theaterId);

    /// <summary>One template with items, TRACKED.</summary>
    Task<ChecklistTemplate?> GetTemplateAsync(Guid id);

    /// <summary>The theater's active template of a kind with items (ordered), untracked, or null.</summary>
    Task<ChecklistTemplate?> GetActiveTemplateAsync(Guid theaterId, ChecklistKind kind);

    /// <summary>True when another active template of the same theater and kind exists.</summary>
    Task<bool> HasActiveTemplateAsync(Guid theaterId, ChecklistKind kind, Guid? excludeTemplateId);

    /// <summary>Stages a new template (with its items) on the context; the caller saves.</summary>
    void StageTemplate(ChecklistTemplate template);

    /// <summary>Stages removal of the template's current items and the addition of <paramref name="items"/>; the caller saves.</summary>
    void StageReplaceTemplateItems(ChecklistTemplate template, IReadOnlyList<ChecklistTemplateItem> items);

    /// <summary>The run of a showtime/room/kind with items (ordered), TRACKED, or null.</summary>
    Task<ChecklistRun?> GetRunAsync(Guid showTimeId, Guid roomId, ChecklistKind kind);

    /// <summary>A run with items (ordered), TRACKED, by the id of one of its items, or null.</summary>
    Task<ChecklistRun?> GetRunByItemAsync(Guid runItemId);

    /// <summary>A run with items (ordered), TRACKED, or null.</summary>
    Task<ChecklistRun?> GetRunByIdAsync(Guid runId);

    /// <summary>Saves a new run with its items immediately.</summary>
    Task AddRunAsync(ChecklistRun run);
}
