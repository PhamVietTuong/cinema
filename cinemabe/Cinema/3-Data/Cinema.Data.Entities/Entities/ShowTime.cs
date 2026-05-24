using Cinema.Data.Enums;
namespace Cinema.Data.Entities;
public class ShowTime : BaseEntity
{
    public Guid MovieId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ProjectionForm ProjectionForm { get; set; } = ProjectionForm.TwoD;
    public ShowTimeType ShowTimeType { get; set; } = ShowTimeType.Normal;
    public bool IsActive { get; set; } = true;
    public Movie Movie { get; set; } = null!;
    public ICollection<ShowTimeRoom> ShowTimeRooms { get; set; } = new List<ShowTimeRoom>();
}
