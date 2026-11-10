namespace Cinema.Data.Contracts;

/// <summary>Filters of the cross-movie public showtime search. <see cref="From"/>/<see cref="To"/> bound the
/// StartTime (half-open); <see cref="TimeFrom"/>/<see cref="TimeTo"/> bound the time of day (inclusive).</summary>
public record ShowTimeSearchFilter(
    Guid? MovieId = null,
    Guid? TheaterId = null,
    Guid? MovieTypeId = null,
    string? Language = null,
    DateTime? From = null,
    DateTime? To = null,
    TimeOnly? TimeFrom = null,
    TimeOnly? TimeTo = null);
