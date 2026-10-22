using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

/// <summary>Removes one physical seat from a room's grid; the seats after it in the same row shift
/// down by one ColIndex so the row stays numbered without a gap.</summary>
public class RemoveSeatRequest
{
    public Guid RoomId { get; set; }
    public Guid SeatId { get; set; }
}
