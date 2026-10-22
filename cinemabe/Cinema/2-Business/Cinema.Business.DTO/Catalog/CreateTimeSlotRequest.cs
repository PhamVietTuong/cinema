
namespace Cinema.Business.DTO.Catalog;

public class CreateTimeSlotRequest
{
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}
