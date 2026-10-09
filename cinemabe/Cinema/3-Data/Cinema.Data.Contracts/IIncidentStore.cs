using Cinema.Data.Entities;
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

/// <summary>An incident plus the room/seat labels it points at (left-joined, so both may be null).</summary>
public record IncidentRow(Incident Incident, string? RoomName, string? SeatRowName, int? SeatColIndex);

/// <summary>One upcoming active ticket that a blocked seat/room affects, flattened for the relocation list.</summary>
public record AffectedTicketRow(
    Guid InvoiceId,
    string InvoiceCode,
    string CustomerName,
    string CustomerPhone,
    Guid ShowTimeId,
    Guid RoomId,
    string RoomName,
    string MovieTitle,
    DateTime StartTime,
    Guid SeatId,
    string SeatRowName,
    int SeatColIndex);

public interface IIncidentStore : IGenericStore<Incident>
{
    /// <summary>Filtered incident page with room/seat labels, newest first, untracked. One page query + one count.</summary>
    Task<(List<IncidentRow> Items, int Total)> SearchAsync(IncidentSearchCriteria criteria);

    /// <summary>One incident with its room/seat labels, untracked, or null.</summary>
    Task<IncidentRow?> GetRowAsync(Guid id);

    /// <summary>
    /// Active (Pending/Paid), not yet used tickets in <paramref name="roomId"/> whose showtime has not ended by
    /// <paramref name="now"/>. <paramref name="seatIds"/> null = the whole room. One projected query.
    /// </summary>
    Task<List<AffectedTicketRow>> GetUpcomingTicketsAsync(Guid roomId, IReadOnlyCollection<Guid>? seatIds, DateTime now);
}
