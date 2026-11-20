using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>One showtime of the admin week calendar: only the columns the grid draws. Projected + untracked.
/// <see cref="RoomId"/> is null (and the buffer 0) when the showtime has no room assigned yet.</summary>
public record ShowTimeCalendarRow(
    Guid Id,
    Guid MovieId,
    DateTime StartTime,
    DateTime EndTime,
    ProjectionForm ProjectionForm,
    ShowTimeType ShowTimeType,
    Guid? RoomId,
    int TurnoverBufferMinutes);
