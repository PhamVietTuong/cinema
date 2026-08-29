using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface IRoomManager : ICatalogManager<RoomDTO, CreateRoomRequest, UpdateRoomRequest>
{
    /// <summary>Returns every seat of a room with its current type + grouping, for the seat-map editor.</summary>
    Task<List<RoomSeatDTO>> GetSeatMapAsync(Guid roomId);
    /// <summary>Persists seat-type and double-seat grouping changes for a room's existing seats.</summary>
    Task SaveSeatMapAsync(SaveSeatMapRequest request);
    /// <summary>Adds/removes rows or columns of seats, preserving the seats that stay in the grid. Returns the updated seat map.</summary>
    Task<List<RoomSeatDTO>> ResizeSeatGridAsync(ResizeSeatGridRequest request);
}
