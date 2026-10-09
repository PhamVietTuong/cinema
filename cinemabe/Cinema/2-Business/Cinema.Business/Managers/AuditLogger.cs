using Cinema.Business.Contracts;
using Cinema.Business.DTO.Staff;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

/// <summary>Stages audit rows on the unit of work; the caller's SaveChanges/transaction persists them.</summary>
public class AuditLogger : IAuditLogger
{
    private readonly IApplicationUnitOfWork _uow;

    public AuditLogger(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public Task LogAsync(AuditEntry entry)
    {
        _uow.AuditLogStore.Stage(new AuditLog
        {
            TheaterId = entry.TheaterId,
            ActorUserId = entry.ActorUserId,
            ApproverUserId = entry.ApproverUserId,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            Amount = entry.Amount,
            ReasonCode = entry.ReasonCode,
            Reason = entry.Reason,
            DataJson = entry.DataJson
        });
        return Task.CompletedTask;
    }
}
