using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

public class SaveSeatMapRequest
{
    public Guid RoomId { get; set; }
    public List<SeatAssignmentItem> Seats { get; set; } = new();
}
