using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>One showtime x room hit of the public showtime search, with the movie and theater it belongs to.</summary>
public record ShowTimeSearchRow(
    Guid ShowTimeId,
    Guid RoomId,
    DateTime StartTime,
    DateTime EndTime,
    ProjectionForm ProjectionForm,
    Guid MovieId,
    string MovieTitle,
    string MovieSlug,
    string? PosterUrl,
    string? Language,
    Guid TheaterId,
    string TheaterName,
    string RoomName,
    string RoomTypeName,
    int BasePrice);
