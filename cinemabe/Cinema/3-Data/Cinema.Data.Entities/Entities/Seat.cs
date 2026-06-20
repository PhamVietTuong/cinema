namespace Cinema.Data.Entities;
public class Seat : BaseEntity
{
    public Guid RoomId { get; set; }
    public string RowName { get; set; } = string.Empty;
    public int ColIndex { get; set; }
    public Guid SeatTypeId { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Links seats that must be sold together (e.g. a double seat = two seats
    /// sharing one group id). Null for an ordinary standalone seat.
    /// </summary>
    public Guid? SeatGroupId { get; set; }

    public Room Room { get; set; } = null!;
    public SeatType SeatType { get; set; } = null!;
}
