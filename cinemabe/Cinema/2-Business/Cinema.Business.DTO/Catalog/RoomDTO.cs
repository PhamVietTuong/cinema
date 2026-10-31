using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

public class RoomDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TheaterId { get; set; }
    public Guid RoomTypeId { get; set; }
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public RoomStatus Status { get; set; }
    /// <summary>Actual bookable seats (active, non-aisle) — not simply TotalRows * TotalColumns, since
    /// an aisle cell occupies a grid slot but isn't a seat.</summary>
    public int SeatCount { get; set; }
}
