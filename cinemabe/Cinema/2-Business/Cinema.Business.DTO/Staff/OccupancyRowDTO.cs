
namespace Cinema.Business.DTO.Staff;

public class OccupancyRowDTO
{
    public Guid ShowTimeId { get; set; }
    public Guid RoomId { get; set; }
    public Guid TheaterId { get; set; }
    public string TheaterName { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string MovieTitle { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int SoldSeats { get; set; }
    public int ActiveSeats { get; set; }
    /// <summary>SoldSeats / ActiveSeats, 0 when the room has no active seats.</summary>
    public double OccupancyRate { get; set; }
}
