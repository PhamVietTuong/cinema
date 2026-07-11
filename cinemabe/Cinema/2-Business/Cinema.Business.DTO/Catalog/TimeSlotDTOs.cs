using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class TimeSlotDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}

public class CreateTimeSlotRequest
{
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}

public class UpdateTimeSlotRequest : IHasId
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}
