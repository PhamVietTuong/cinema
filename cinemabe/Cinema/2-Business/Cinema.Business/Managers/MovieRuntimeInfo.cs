namespace Cinema.Business.Managers;

/// <summary>Snapshot of the movie properties this manager validates a showtime's window and
/// availability against. Public for the same reason as <see cref="RoomClassInfo"/> — the test
/// project's mock setup needs to name the store's <c>FindSelectAsync&lt;Class&gt;</c> explicit
/// type argument.</summary>
public sealed record MovieRuntimeInfo(string Title, int Duration, bool IsActive, DateOnly ReleaseDate, DateOnly? EndDate);
