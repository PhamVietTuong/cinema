using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Staff;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.BoxOffice;

public class CounterShowtimeDTO
{
    public Guid ShowTimeId { get; set; }
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public Guid MovieId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ProjectionForm ProjectionForm { get; set; }
    /// <summary>The showtime has finished and can no longer be sold.</summary>
    public bool HasEnded { get; set; }
}
