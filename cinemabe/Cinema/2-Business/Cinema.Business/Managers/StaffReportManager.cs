using Cinema.Business.Contracts;
using Cinema.Business.DTO.Requests;
using Cinema.Business.DTO.Staff;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class StaffReportManager : IStaffReportManager
{
    private readonly IApplicationUnitOfWork _uow;

    public StaffReportManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<DefaultSearchResults<AuditLogDTO>> GetAuditLogAsync(PagingSearchDTO search, IReadOnlyCollection<Guid>? theaterIds)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var filters = search.Filters;

        var to = filters.GetDateTime("to");
        if (to.HasValue && to.Value.TimeOfDay == TimeSpan.Zero)
        {
            // A bare date means "through the end of that day" (SQL datetime resolution is ~3 ms).
            to = to.Value.AddDays(1).AddMilliseconds(-3);
        }

        var criteria = new AuditLogSearchCriteria(
            TheaterIds: theaterIds,
            Action: filters.GetEnum<AuditAction>("action"),
            From: filters.GetDateTime("from"),
            To: to,
            ActorUserId: filters.GetGuid("actorId"),
            PageIndex: page - 1,
            PageSize: pageSize);

        var (rows, total) = await _uow.AuditLogStore.SearchAsync(criteria);

        // One batched id -> name lookup for the whole page (no per-row query).
        var userIds = rows.Select(r => r.ActorUserId)
            .Concat(rows.Where(r => r.ApproverUserId.HasValue).Select(r => r.ApproverUserId!.Value))
            .Distinct()
            .ToList();
        var names = await _uow.UserStore.GetNamesByIdsAsync(userIds);

        var results = rows.Select(r => new AuditLogDTO
        {
            Id = r.Id,
            TheaterId = r.TheaterId,
            ActorUserId = r.ActorUserId,
            ActorName = names.TryGetValue(r.ActorUserId, out var actorName) ? actorName : null,
            ApproverUserId = r.ApproverUserId,
            ApproverName = r.ApproverUserId.HasValue && names.TryGetValue(r.ApproverUserId.Value, out var approverName) ? approverName : null,
            Action = r.Action,
            EntityType = r.EntityType,
            EntityId = r.EntityId,
            Amount = r.Amount,
            ReasonCode = r.ReasonCode,
            Reason = r.Reason,
            DataJson = r.DataJson,
            CreationTime = r.CreationTime
        }).ToList();

        return new DefaultSearchResults<AuditLogDTO>
        {
            Results = results,
            TotalCount = total,
            CountPerPage = pageSize,
            Page = page
        };
    }
}
