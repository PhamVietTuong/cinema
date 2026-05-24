using Cinema.Data.Enums;
namespace Cinema.Data.Entities;
public class Room : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid TheaterId { get; set; }
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Active;
    public Theater Theater { get; set; } = null!;
    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public ICollection<ShowTimeRoom> ShowTimeRooms { get; set; } = new List<ShowTimeRoom>();
}
