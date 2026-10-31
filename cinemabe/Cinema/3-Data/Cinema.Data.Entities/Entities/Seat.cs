namespace Cinema.Data.Entities;
public class Seat : BaseEntity
{
    public Guid RoomId { get; set; }
    public string RowName { get; set; } = string.Empty;
    public int ColIndex { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Stores only Standard or Aisle. Double is never stored here — it is still derived from
    /// <see cref="SeatGroupId"/> — so use <c>SeatKinds.EffectiveKind</c> to read the real kind.
    /// An aisle always has <see cref="SeatGroupId"/> null and <see cref="IsActive"/> true.
    /// </summary>
    public SeatKind Kind { get; set; } = SeatKind.Standard;

    /// <summary>
    /// Links seats that must be sold together (e.g. a double seat = two seats sharing one group id).
    /// Null for an ordinary standalone seat. This is the sole source of truth for Standard vs Double —
    /// there is no separate SeatTypeId on Seat, to avoid two places that could disagree.
    /// </summary>
    public Guid? SeatGroupId { get; set; }

    public Room Room { get; set; } = null!;
}
