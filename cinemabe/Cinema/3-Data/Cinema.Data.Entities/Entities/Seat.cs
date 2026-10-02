namespace Cinema.Data.Entities;
public class Seat : BaseEntity
{
    public Guid RoomId { get; set; }
    public string RowName { get; set; } = string.Empty;
    public int ColIndex { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Links seats that must be sold together (e.g. a double seat = two seats sharing one group id).
    /// Null for an ordinary standalone seat. This is also the sole source of truth for the seat's
    /// kind (Standard vs Double) — there is no separate SeatTypeId on Seat, to avoid two places that
    /// could disagree about whether a seat is a double.
    /// </summary>
    public Guid? SeatGroupId { get; set; }

    public Room Room { get; set; } = null!;
}
