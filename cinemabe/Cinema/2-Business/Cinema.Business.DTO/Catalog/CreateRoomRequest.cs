using Cinema.Business.DTO.Requests;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

public class CreateRoomRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid TheaterId { get; set; }
    public Guid RoomTypeId { get; set; }
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Active;
}
