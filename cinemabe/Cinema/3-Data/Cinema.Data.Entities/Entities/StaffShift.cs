namespace Cinema.Data.Entities;

/// <summary>A rostered working period of one staff member in a theater. Times are the theater's local wall-clock times (like showtimes).</summary>
public class StaffShift : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Note { get; set; }
}
