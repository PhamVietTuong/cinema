using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

public class PromotionProgramPriceDTO
{
    /// <summary>Null = any format.</summary>
    public ProjectionForm? ProjectionForm { get; set; }
    /// <summary>Null = any seat kind.</summary>
    public SeatKind? SeatKind { get; set; }
    public double Price { get; set; }
}

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

public class PromotionProgramDTO
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? BannerUrl { get; set; }

    public PromotionMechanic Mechanic { get; set; }
    public double? PercentOff { get; set; }

    public PromotionEligibility Eligibility { get; set; }
    public Guid? MinMemberShipId { get; set; }
    public string? MinMemberShipName { get; set; }
    public int? MaxAge { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool ExcludeHolidays { get; set; }
    /// <summary>Bitmask of SalesChannel values; null = all channels.</summary>
    public int? AllowedChannelsMask { get; set; }
    public int? MaxTicketsPerTransaction { get; set; }
    public bool StacksWithMembershipDiscount { get; set; }
    public string? TermsText { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    public bool AutoApply { get; set; }

    public Guid? LinkedDiscountId { get; set; }
    /// <summary>Shown only when the linked discount is active and has a code.</summary>
    public string? LinkedDiscountCode { get; set; }

    public List<PromotionProgramPriceDTO> Prices { get; set; } = new();
    public List<PromotionProgramScheduleDTO> Schedules { get; set; } = new();
}

public class CreatePromotionProgramRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Summary { get; set; }
    public string? BannerUrl { get; set; }

    public PromotionMechanic Mechanic { get; set; }
    public double? PercentOff { get; set; }

    public PromotionEligibility Eligibility { get; set; }
    public Guid? MinMemberShipId { get; set; }
    public int? MaxAge { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool ExcludeHolidays { get; set; }
    public int? AllowedChannelsMask { get; set; }
    public int? MaxTicketsPerTransaction { get; set; }
    public bool StacksWithMembershipDiscount { get; set; }

    [StringLength(2000)]
    public string? TermsText { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    public bool AutoApply { get; set; }
    public Guid? LinkedDiscountId { get; set; }

    public List<PromotionProgramPriceDTO> Prices { get; set; } = new();
    public List<PromotionProgramScheduleDTO> Schedules { get; set; } = new();
}

public class UpdatePromotionProgramRequest : IHasId
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Summary { get; set; }
    public string? BannerUrl { get; set; }

    public PromotionMechanic Mechanic { get; set; }
    public double? PercentOff { get; set; }

    public PromotionEligibility Eligibility { get; set; }
    public Guid? MinMemberShipId { get; set; }
    public int? MaxAge { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool ExcludeHolidays { get; set; }
    public int? AllowedChannelsMask { get; set; }
    public int? MaxTicketsPerTransaction { get; set; }
    public bool StacksWithMembershipDiscount { get; set; }

    [StringLength(2000)]
    public string? TermsText { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    public bool AutoApply { get; set; }
    public Guid? LinkedDiscountId { get; set; }

    public List<PromotionProgramPriceDTO> Prices { get; set; } = new();
    public List<PromotionProgramScheduleDTO> Schedules { get; set; } = new();
}
