using Cinema.Business.DTO.Requests;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

public class RoomDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TheaterId { get; set; }
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public RoomStatus Status { get; set; }
}

public class CreateRoomRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid TheaterId { get; set; }
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Active;
}

public class UpdateRoomRequest : IHasId
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TheaterId { get; set; }
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Active;
}
