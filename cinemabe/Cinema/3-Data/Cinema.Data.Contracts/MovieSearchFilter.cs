namespace Cinema.Data.Contracts;

/// <summary>Server-side filters of the public movie list. Every member is optional; unset means "no filter".
/// <see cref="ShowFrom"/>/<see cref="ShowTo"/> (half-open) and <see cref="TheaterId"/> restrict to movies that have
/// an active showtime in that window (and theater). <see cref="Status"/> is "now" or "coming".</summary>
public record MovieSearchFilter(
    string? Search = null,
    string? Director = null,
    Guid? MovieTypeId = null,
    string? Language = null,
    Guid? TheaterId = null,
    DateTime? ShowFrom = null,
    DateTime? ShowTo = null,
    string? Status = null);
