namespace Cinema.Data.Entities;
public class Seat : BaseEntity
{
    public Guid RoomId { get; set; }
    public string RowName { get; set; } = string.Empty;
    public int ColIndex { get; set; }
    public Guid SeatTypeId { get; set; }
    public bool IsActive { get; set; } = true;
    public Room Room { get; set; } = null!;
    public SeatType SeatType { get; set; } = null!;
}
