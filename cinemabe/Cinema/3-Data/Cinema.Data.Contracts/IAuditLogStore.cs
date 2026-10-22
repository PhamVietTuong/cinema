using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

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
