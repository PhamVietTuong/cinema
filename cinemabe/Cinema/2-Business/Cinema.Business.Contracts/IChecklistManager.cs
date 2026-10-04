using Cinema.Business.DTO.Operations;

namespace Cinema.Business.Contracts;

/// <summary>
/// Showtime checklists. <c>theaterId</c> arguments are already resolved against the caller's scope by the
/// controller; <c>scopeTheaterIds</c> (null = every theater) refuses runs/templates of a theater outside the scope
/// (<c>AccessDeniedException</c>, 403). Template management is for approvers (enforced on the controller).
/// </summary>
public interface IChecklistManager
{
    Task<List<ChecklistTemplateDTO>> GetTemplatesAsync(Guid theaterId);

    /// <summary>Creates or edits a template. Only one active template per theater and kind (400 otherwise).</summary>
    Task<ChecklistTemplateDTO> SaveTemplateAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid theaterId, SaveChecklistTemplateRequest request);

    /// <summary>
    /// Opens the checklist of a showtime/room. The run is created lazily, as a copy of the active template, the first
    /// time it is opened (no background job); later opens return the same run. 404 when no active template exists.
    /// </summary>
    Task<ChecklistRunDTO> OpenAsync(Guid theaterId, OpenChecklistRequest request);

    Task<ChecklistRunDTO> SetItemAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid actorUserId, SetChecklistItemRequest request);

    /// <summary>Completes a run; every required item must be done first (400 otherwise).</summary>
    Task<ChecklistRunDTO> CompleteAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid actorUserId, CompleteChecklistRequest request);
}
