using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>One room of a <see cref="ShowTimeListRow"/>.</summary>
public record ShowTimeListRoomRow(Guid RoomId, string RoomName, string RoomTypeName, string TheaterName, double BasePrice);

/// <summary>A showtime with its rooms for the public "showtimes of a movie on a day" list. Projected + untracked.</summary>
public record ShowTimeListRow(
    Guid Id,
    DateTime StartTime,
    DateTime EndTime,
    ProjectionForm ProjectionForm,
    ShowTimeType ShowTimeType,
    IReadOnlyList<ShowTimeListRoomRow> Rooms);
