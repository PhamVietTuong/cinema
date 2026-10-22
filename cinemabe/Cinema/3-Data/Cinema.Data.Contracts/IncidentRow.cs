using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

/// <summary>An incident plus the room/seat labels it points at (left-joined, so both may be null).</summary>
public record IncidentRow(Incident Incident, string? RoomName, string? SeatRowName, int? SeatColIndex);
