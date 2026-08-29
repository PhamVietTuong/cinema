using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class DiscountDTO
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public double Percent { get; set; }
    public double? MaxDiscountAmount { get; set; }
    public Guid DiscountTypeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? MaxUsage { get; set; }
    public bool IsActive { get; set; }

    // ── Promotion scope ──────────────────────────────────────────────
    public bool AutoApply { get; set; }
    public bool ApplyToAllTheaters { get; set; }
    /// <summary>Theaters the promotion is limited to (empty when ApplyToAllTheaters is true).</summary>
    public List<Guid> TheaterIds { get; set; } = new();
    public Guid? MovieId { get; set; }
    /// <summary>Day-of-week bitmask (bit 0 = Sunday … bit 6 = Saturday); null = any day.</summary>
    public int? DaysOfWeekMask { get; set; }
    /// <summary>Daily start time "HH:mm"; null = any time.</summary>
    public string? StartTimeOfDay { get; set; }
    /// <summary>Daily end time "HH:mm"; null = any time.</summary>
    public string? EndTimeOfDay { get; set; }
}

public class CreateDiscountRequest
{
    public string? Code { get; set; }
    public string? Description { get; set; }
    public double Percent { get; set; }
    public double? MaxDiscountAmount { get; set; }
    public Guid DiscountTypeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? MaxUsage { get; set; }
    public bool IsActive { get; set; } = true;

    public bool AutoApply { get; set; }
    public bool ApplyToAllTheaters { get; set; } = true;
    public List<Guid> TheaterIds { get; set; } = new();
    public Guid? MovieId { get; set; }
    public int? DaysOfWeekMask { get; set; }
    public string? StartTimeOfDay { get; set; }
    public string? EndTimeOfDay { get; set; }
}

public class UpdateDiscountRequest : IHasId
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public double Percent { get; set; }
    public double? MaxDiscountAmount { get; set; }
    public Guid DiscountTypeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? MaxUsage { get; set; }
    public bool IsActive { get; set; } = true;

    public bool AutoApply { get; set; }
    public bool ApplyToAllTheaters { get; set; } = true;
    public List<Guid> TheaterIds { get; set; } = new();
    public Guid? MovieId { get; set; }
    public int? DaysOfWeekMask { get; set; }
    public string? StartTimeOfDay { get; set; }
    public string? EndTimeOfDay { get; set; }
}
