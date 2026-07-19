namespace Cinema.Data.Entities;
public class Discount : BaseEntity
{
    /// <summary>Optional promo code. Null/empty for an auto-applied promotion.</summary>
    public string? Code { get; set; }
    public string? Description { get; set; }
    public double Percent { get; set; }
    public double? MaxDiscountAmount { get; set; }
    public Guid DiscountTypeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? MaxUsage { get; set; }
    public int UsedCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    /// <summary>When true the promotion is applied automatically to any matching booking (no code required).</summary>
    public bool AutoApply { get; set; } = false;
    /// <summary>When true the promotion applies to every theater; otherwise only the theaters in <see cref="DiscountTheaters"/>.</summary>
    public bool ApplyToAllTheaters { get; set; } = true;
    /// <summary>Optional movie scope; null = applies to any movie.</summary>
    public Guid? MovieId { get; set; }
    /// <summary>Optional day-of-week scope as a bitmask (bit 0 = Sunday … bit 6 = Saturday); null = any day.</summary>
    public int? DaysOfWeekMask { get; set; }
    /// <summary>Optional daily time window (inclusive) the showtime must start within; null = any time.</summary>
    public TimeOnly? StartTimeOfDay { get; set; }
    public TimeOnly? EndTimeOfDay { get; set; }

    public DiscountType DiscountType { get; set; } = null!;
    public Movie? Movie { get; set; }
    /// <summary>Theaters this promotion is limited to when <see cref="ApplyToAllTheaters"/> is false.</summary>
    public ICollection<DiscountTheater> DiscountTheaters { get; set; } = new List<DiscountTheater>();
}
