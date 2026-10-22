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
