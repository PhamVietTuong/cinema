using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>Flattened ShowTime x Room row for the movie-detail schedule strip. Projected + untracked:
/// one query, only the columns that page renders.</summary>
public record MovieScheduleRow(
    Guid ShowTimeId,
    DateTime StartTime,
    DateTime EndTime,
    ProjectionForm ProjectionForm,
    Guid RoomId,
    string RoomName,
    string RoomTypeName,
    Guid TheaterId,
    string TheaterName,
    string TheaterAddress,
    int Capacity);
