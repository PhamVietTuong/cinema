using Cinema.Business.DTO.Staff;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Operations;

public class ScheduleBoardShowTimeDTO
{
    public Guid ShowTimeId { get; set; }
    public Guid MovieId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    /// <summary>End plus the room class's turnover buffer: when the room is free for the next screening.</summary>
    public DateTime BufferEnd { get; set; }
    public int Sold { get; set; }
    public int Capacity { get; set; }
    public RoomStatus RoomStatus { get; set; }
}
