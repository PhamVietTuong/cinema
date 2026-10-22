using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>Filter + page for incidents (newest first). PageIndex is 0-based. TheaterIds null = all theaters.</summary>
public record IncidentSearchCriteria(
    IReadOnlyCollection<Guid>? TheaterIds,
    IncidentStatus? Status,
    IncidentCategory? Category,
    DateTime? From,
    DateTime? To,
    int PageIndex,
    int PageSize);
