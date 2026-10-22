using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>One ShowTime x Room row of a theater for the box-office showtime picker. Projected + untracked.</summary>
public record TheaterShowTimeRow(
    Guid ShowTimeId,
    Guid RoomId,
    string RoomName,
    Guid MovieId,
    string MovieTitle,
    DateTime StartTime,
    DateTime EndTime,
    ProjectionForm ProjectionForm);
