using Cinema.Business.DTO.Staff;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Operations;

public class ScheduleBoardDTO
{
    public Guid TheaterId { get; set; }
    public DateTime Date { get; set; }
    public List<ScheduleBoardRoomDTO> Rooms { get; set; } = new();
}
