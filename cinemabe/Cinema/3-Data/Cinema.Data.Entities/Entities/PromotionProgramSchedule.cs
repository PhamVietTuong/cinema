namespace Cinema.Data.Entities;

/// <summary>One "where and when" validity window for a PromotionProgram. A program with zero schedule
/// rows applies at every theater, every day, every time. Each row narrows that: null TheaterId means
/// "any theater" for that row, null DaysOfWeekMask/time window means "any day"/"any time" for that row.
/// A program needs several rows to express "theater X on Mon-Fri" plus "theater Y on weekends", etc.</summary>
public class PromotionProgramSchedule : BaseEntity
{
    public Guid PromotionProgramId { get; set; }
    public PromotionProgram PromotionProgram { get; set; } = null!;

    /// <summary>Null = all theaters.</summary>
    public Guid? TheaterId { get; set; }
    public Theater? Theater { get; set; }

    /// <summary>Bitmask, bit 0 = Sunday … bit 6 = Saturday (same convention as Discount.DaysOfWeekMask).
    /// Null = any day.</summary>
    public int? DaysOfWeekMask { get; set; }
    /// <summary>Inclusive daily time window the showtime must start within. Null = any time.</summary>
    public TimeOnly? StartTimeOfDay { get; set; }
    public TimeOnly? EndTimeOfDay { get; set; }
}
