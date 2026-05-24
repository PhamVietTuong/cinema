using Cinema.Data.Enums;
namespace Cinema.Business.DTO.Movies;
public class ShowTimeSummaryDTO
{
    public Guid Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ProjectionForm ProjectionForm { get; set; }
    public string TheaterName { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public int AvailableSeats { get; set; }
}
