namespace Cinema.Data.Entities;

/// <summary>
/// A theater a RegionalManager is assigned to (decision D12: an assignment table, not a Region entity).
/// Composite key (UserId, TheaterId). Other roles use <c>User.TheaterId</c> instead.
/// </summary>
public class UserTheater
{
    public Guid UserId { get; set; }
    public Guid TheaterId { get; set; }
    public User User { get; set; } = null!;
}
