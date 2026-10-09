using Cinema.Business.DTO.Staff;
using Cinema.Data.Enums;

namespace Cinema.Business.Contracts;

/// <summary>
/// Manager PIN override for sensitive staff actions. Failure handling is persisted by the service itself
/// (failed-attempt counter, lockout and the OverrideFailed audit row are saved immediately), so call
/// <see cref="VerifyAsync"/> BEFORE the caller opens its own transaction, otherwise a rollback would erase the
/// failure record.
/// </summary>
public interface IManagerOverrideService
{
    /// <summary>
    /// Authorises a sensitive action in a theater and returns the approver's user id.
    /// An actor who already holds an approver role for that theater needs no PIN: their own id is returned.
    /// Otherwise <paramref name="overrideDto"/> is required and the approver must hold an Approvers role in the
    /// same theater (Admin: any theater). Throws <c>AccessDeniedException</c> (403) when the override is missing,
    /// the approver is out of scope, the PIN is wrong or the PIN is locked out (5 consecutive failures lock it
    /// for 15 minutes). Every rejected attempt is audited as <c>AuditAction.OverrideFailed</c>.
    /// The caller audits the successful action itself (via <c>IAuditLogger</c>) using the returned approver id.
    /// </summary>
    Task<Guid> VerifyAsync(Guid theaterId, Guid actorUserId, ManagerOverrideDTO? overrideDto, AuditAction action);

    /// <summary>Sets/replaces a user's override PIN (4 to 8 digits, salted PBKDF2 hash) and clears any lockout.</summary>
    Task SetPinAsync(Guid userId, string pin);

    /// <summary>Active approvers (Id, Name) who could authorise an override in the theater.</summary>
    Task<List<OverrideApproverDTO>> GetApproversAsync(Guid theaterId);
}
