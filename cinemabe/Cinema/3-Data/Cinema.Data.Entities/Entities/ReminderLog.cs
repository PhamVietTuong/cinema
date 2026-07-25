namespace Cinema.Data.Entities;

/// <summary>
/// Records that a showtime reminder was sent to a user for a showtime, so the reminder job doesn't
/// re-send after a process restart (it replaces the previous in-memory dedup set). Unique on
/// (UserId, ShowTimeId).
/// </summary>
public class ReminderLog : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ShowTimeId { get; set; }
    public DateTime SentAt { get; set; }
}
