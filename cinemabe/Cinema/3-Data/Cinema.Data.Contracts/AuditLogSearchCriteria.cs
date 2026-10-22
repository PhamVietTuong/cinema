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
