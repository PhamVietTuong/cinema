using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>A room of the theater with its class buffer and its sellable (active) seat count.</summary>
public record BoardRoomRow(Guid RoomId, string RoomName, string RoomTypeName, RoomStatus Status, int TurnoverBufferMinutes, int Capacity);
