using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

public class PromotionProgramScheduleDTO
{
    /// <summary>Null = all theaters.</summary>
    public Guid? TheaterId { get; set; }
    public string? TheaterName { get; set; }
    /// <summary>Day-of-week bitmask (bit 0 = Sunday … bit 6 = Saturday); null = any day.</summary>
    public int? DaysOfWeekMask { get; set; }
    /// <summary>Daily start time "HH:mm"; null = any time.</summary>
    public string? StartTimeOfDay { get; set; }
    /// <summary>Daily end time "HH:mm"; null = any time.</summary>
    public string? EndTimeOfDay { get; set; }
}
