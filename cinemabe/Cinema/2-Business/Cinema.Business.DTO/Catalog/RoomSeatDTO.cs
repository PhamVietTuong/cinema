using Cinema.Business.DTO.Requests;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

// ── Seat-map editor (group/ungroup double seats) ───────────────────
public class RoomSeatDTO
{
    public Guid Id { get; set; }
    public string RowName { get; set; } = string.Empty;
    public int ColIndex { get; set; }
    /// <summary>Derived from SeatGroupId being set — a seat has no stored kind of its own.</summary>
    public bool IsDouble { get; set; }
    public Guid? SeatGroupId { get; set; }
    public bool IsActive { get; set; } = true;
}
