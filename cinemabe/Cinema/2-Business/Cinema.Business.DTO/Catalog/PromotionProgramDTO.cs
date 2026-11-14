using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

public class PromotionProgramDTO
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? BannerUrl { get; set; }

    public Guid? CampaignId { get; set; }
    public string? CampaignName { get; set; }

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
    public string? TermsText { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    public bool AutoApply { get; set; }

    public string? Code { get; set; }
    public int? MaxUsage { get; set; }
    public int UsedCount { get; set; }
    public double? MaxDiscountAmount { get; set; }
    public Guid? MovieId { get; set; }
    public string? MovieTitle { get; set; }

    public List<PromotionProgramPriceDTO> Prices { get; set; } = new();
    public List<PromotionProgramScheduleDTO> Schedules { get; set; } = new();
}
