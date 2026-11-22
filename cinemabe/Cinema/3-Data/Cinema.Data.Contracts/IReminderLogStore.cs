using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IReminderLogStore : IGenericStore<ReminderLog>
{
    /// <summary>Whether a reminder has already been recorded for this (user, showtime).</summary>
    Task<bool> WasSentAsync(Guid userId, Guid showTimeId);

    /// <summary>The (user, showtime) pairs among the given showtimes that already have a reminder recorded — one query for a whole batch.</summary>
    Task<HashSet<(Guid UserId, Guid ShowTimeId)>> GetSentAmongAsync(IReadOnlyCollection<Guid> showTimeIds);
}
