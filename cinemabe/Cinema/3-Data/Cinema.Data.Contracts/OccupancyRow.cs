namespace Cinema.Data.Contracts;

public class OccupancyRow
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
}
