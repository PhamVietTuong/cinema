using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Movies;

public class ShowTimeListDTO
{
    public Guid            Id             { get; set; }
    public DateTime        StartTime      { get; set; }
    public DateTime        EndTime        { get; set; }
    public ProjectionForm  ProjectionForm { get; set; }
    public ShowTimeType    ShowTimeType   { get; set; }
    public List<ShowTimeRoomDTO> Rooms   { get; set; } = [];
}

public class ShowTimeRoomDTO
{
    public Guid     RoomId      { get; set; }
    public string?  RoomName    { get; set; }
    public string?  RoomTypeName { get; set; }
    public string?  TheaterName { get; set; }
    public double  BasePrice   { get; set; }
}
