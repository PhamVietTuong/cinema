using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>Filter + page for the audit trail (newest first). Page is 0-based. TheaterIds null = all theaters.</summary>
public record AuditLogSearchCriteria(
    IReadOnlyCollection<Guid>? TheaterIds,
    AuditAction? Action,
    DateTime? From,
    DateTime? To,
    Guid? ActorUserId,
    int PageIndex,
    int PageSize);

public interface IAuditLogStore : IGenericStore<AuditLog>
{
    /// <summary>
    /// Tracks the row on the context WITHOUT saving (unlike <c>CreateAsync</c>), so it is persisted by the
    /// caller's own SaveChanges, in the caller's transaction.
    /// </summary>
    void Stage(AuditLog entry);

    /// <summary>Filtered audit page, newest first, untracked. Returns the page plus the unpaged total.</summary>
    Task<(List<AuditLog> Items, int Total)> SearchAsync(AuditLogSearchCriteria criteria);
}
