using Cinema.Business.DTO.Requests;
using Cinema.Business.DTO.Staff;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

/// <summary>Scoped staff-facing reports. Only the audit trail exists so far; later phases add sales/occupancy/KPIs.</summary>
public interface IStaffReportManager
{
    /// <summary>
    /// Audit trail page, newest first. <paramref name="theaterIds"/> null = every theater (admin); otherwise only
    /// those theaters. Filters: action, from, to (a bare date means through the end of that day), actorId.
    /// </summary>
    Task<DefaultSearchResults<AuditLogDTO>> GetAuditLogAsync(PagingSearchDTO search, IReadOnlyCollection<Guid>? theaterIds);
}
