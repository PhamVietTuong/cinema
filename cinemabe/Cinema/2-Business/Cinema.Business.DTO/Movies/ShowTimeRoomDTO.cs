
namespace Cinema.Business.DTO.Movies;

public class ShowTimeRoomDTO
{
    public Guid     RoomId      { get; set; }
    public string?  RoomName    { get; set; }
    public string?  RoomTypeName { get; set; }
    public string?  TheaterName { get; set; }
    public double  BasePrice   { get; set; }
}
