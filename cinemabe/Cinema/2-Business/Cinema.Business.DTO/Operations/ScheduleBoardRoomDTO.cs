using Cinema.Business.DTO.Staff;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Operations;

public class ScheduleBoardRoomDTO
{
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public RoomStatus RoomStatus { get; set; }
    /// <summary>Cleaning/turnover minutes the room class needs after each screening.</summary>
    public int TurnoverBufferMinutes { get; set; }
    /// <summary>Sellable (active) seats of the room.</summary>
    public int Capacity { get; set; }
    public List<ScheduleBoardShowTimeDTO> ShowTimes { get; set; } = new();
}
