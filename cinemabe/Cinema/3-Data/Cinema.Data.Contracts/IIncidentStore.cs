using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

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
