using Cinema.Business.DTO.Staff;

namespace Cinema.Business.Contracts;

/// <summary>
/// Records a sensitive staff action. Contract: <see cref="LogAsync"/> only STAGES the audit row on the unit of
/// work. It never calls SaveChanges and never opens/commits a transaction, so the row is committed (or rolled back)
/// together with the action it describes by the caller's own <c>SaveChangesAsync</c>/<c>CommitTransactionAsync</c>.
/// Call it inside the caller's transaction, before that save.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(AuditEntry entry);
}
