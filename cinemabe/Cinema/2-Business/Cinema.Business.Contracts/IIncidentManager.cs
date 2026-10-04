using Cinema.Business.DTO.Operations;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

/// <summary>
/// Staff incident reports and the seat/room blocks they can trigger. The <c>theaterId</c> arguments are already
/// resolved against the caller's scope by the controller; <c>scopeTheaterIds</c> (null = every theater) is used to
/// refuse an incident that belongs to a theater outside the caller's scope (<c>AccessDeniedException</c>, 403).
/// </summary>
public interface IIncidentManager
{
    Task<IncidentDTO> ReportAsync(Guid theaterId, Guid actorUserId, ReportIncidentRequest request);

    /// <summary>Closes an incident. With <c>Unblock</c> it also reopens the blocked seat/room (approver or override only).</summary>
    Task<IncidentDTO> ResolveAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid actorUserId, ResolveIncidentRequest request);

    Task<IncidentDTO> GetAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid incidentId);

    /// <summary>Incident page, newest first. Filters: status, category, from, to.</summary>
    Task<DefaultSearchResults<IncidentDTO>> SearchAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, PagingSearchDTO search);

    /// <summary>
    /// Sets <c>Seat.IsActive=false</c> (both halves of a double seat). Approvers only, or with a manager override;
    /// audited as <c>AuditAction.BlockSeat</c>. Returns the affected upcoming tickets; cancels nothing.
    /// </summary>
    Task<BlockResultDTO> BlockSeatAsync(Guid theaterId, Guid actorUserId, BlockSeatRequest request);

    /// <summary>
    /// Sets <c>Room.Status=Maintenance</c>. Approvers only, or with a manager override; audited as
    /// <c>AuditAction.BlockRoom</c>. Returns the affected upcoming tickets; cancels nothing.
    /// </summary>
    Task<BlockResultDTO> BlockRoomAsync(Guid theaterId, Guid actorUserId, BlockRoomRequest request);
}
