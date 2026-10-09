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

    // ── P9 reporting. <paramref name="scope"/> = the caller's theaters (null = every theater, i.e. admin); a requested
    // theater outside it throws AccessDeniedException (403). An empty scope reports nothing. Range: at most 92 days. ──

    /// <summary>Sales grouped by <see cref="StaffReportRequest.GroupBy"/>, ticket and F&amp;B revenue separate, net of refunds.</summary>
    Task<SalesReportDTO> GetSalesAsync(StaffReportRequest request, IReadOnlyCollection<Guid>? scope);

    /// <summary>Per screening sold seats / active seats for the screenings starting in the range.</summary>
    Task<OccupancyReportDTO> GetOccupancyAsync(StaffReportRequest request, IReadOnlyCollection<Guid>? scope);

    /// <summary>Attach rate, refund rate (count and amount) and average spend per head.</summary>
    Task<StaffKpisDTO> GetKpisAsync(StaffReportRequest request, IReadOnlyCollection<Guid>? scope);
}
