using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IReminderLogStore : IGenericStore<ReminderLog>
{
    /// <summary>Whether a reminder has already been recorded for this (user, showtime).</summary>
    Task<bool> WasSentAsync(Guid userId, Guid showTimeId);
}
